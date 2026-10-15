using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Auth.DTOs;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Auth.Services;

public class AuthService
{
    private readonly IApplicationDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOtpSender _otpSender;
    private readonly OtpOptions _otpOptions;
    private readonly ILogger<AuthService> _logger;

    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public AuthService(
        IApplicationDbContext db,
        ITokenService tokenService,
        IPasswordHasher passwordHasher,
        IOtpSender otpSender,
        OtpOptions otpOptions,
        ILogger<AuthService> logger)
    {
        _db = db;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _otpSender = otpSender;
        _otpOptions = otpOptions;
        _logger = logger;
    }

    /// <summary>
    /// Step 1 of registration: validate the payload, generate a 4-digit code, persist it
    /// (hashed, with the pending registration data) and send it over WhatsApp.
    /// The CustomerUser is not created until the code is confirmed.
    /// </summary>
    public async Task<Result<RegistrationStartResponseDto>> StartRegistrationAsync(
        CustomerRegisterDto dto, CancellationToken ct = default)
    {
        var normalizedMobile = NormalizeMobile(dto.MobileNumber);

        var alreadyRegistered = await _db.CustomerUsers
            .AnyAsync(u => u.MobileNumber == normalizedMobile, ct);
        if (alreadyRegistered)
            return Result<RegistrationStartResponseDto>.Failure("Mobile number already registered.", 409);

        var now = DateTime.UtcNow;
        var pending = await _db.PhoneVerifications
            .FirstOrDefaultAsync(v => v.MobileNumber == normalizedMobile
                && v.Purpose == VerificationPurpose.Registration, ct);

        if (pending != null &&
            pending.LastSentAt.AddSeconds(_otpOptions.ResendCooldownSeconds) > now)
        {
            var wait = (int)Math.Ceiling(
                (pending.LastSentAt.AddSeconds(_otpOptions.ResendCooldownSeconds) - now).TotalSeconds);
            return Result<RegistrationStartResponseDto>.Failure(
                $"Please wait {wait}s before requesting another code.", 429);
        }

        var code = Random.Shared.Next(0, 10000).ToString("D4");
        var expiresAt = now.AddMinutes(_otpOptions.CodeExpiryMinutes);

        if (pending == null)
        {
            pending = new PhoneVerification
            {
                MobileNumber = normalizedMobile,
                Purpose = VerificationPurpose.Registration,
            };
            _db.PhoneVerifications.Add(pending);
        }

        pending.CodeHash = _passwordHasher.Hash(code);
        pending.ExpiresAt = expiresAt;
        pending.LastSentAt = now;
        pending.Attempts = 0;
        pending.PinHash = _passwordHasher.Hash(dto.Pin4);
        pending.FirstName = dto.FirstName.Trim();
        pending.LastName = dto.LastName.Trim();

        await _db.SaveChangesAsync(ct);

        if (_otpOptions.Enabled)
        {
            try
            {
                await _otpSender.SendAsync(normalizedMobile, code, ct);
            }
            catch (OtpSendException ex)
            {
                _logger.LogError(ex, "Failed to send OTP to {Mobile}", normalizedMobile);
                return Result<RegistrationStartResponseDto>.Failure(
                    "Could not send the verification code. Please try again.", 502);
            }
        }
        else
        {
            _logger.LogWarning(
                "OTP delivery disabled — verification code for {Mobile} is {Code}", normalizedMobile, code);
        }

        return Result<RegistrationStartResponseDto>.Success(
            new RegistrationStartResponseDto(
                normalizedMobile, expiresAt, _otpOptions.Enabled ? null : code));
    }

    /// <summary>
    /// Step 2 of registration: confirm the code, create the CustomerUser and issue tokens.
    /// </summary>
    public async Task<Result<AuthResponseDto>> VerifyRegistrationAsync(
        RegisterVerifyDto dto, string? ip = null, CancellationToken ct = default)
    {
        var normalizedMobile = NormalizeMobile(dto.MobileNumber);

        var pending = await _db.PhoneVerifications
            .FirstOrDefaultAsync(v => v.MobileNumber == normalizedMobile
                && v.Purpose == VerificationPurpose.Registration, ct);

        if (pending == null)
            return Result<AuthResponseDto>.Failure("No pending verification. Start registration again.", 400);

        if (pending.ExpiresAt <= DateTime.UtcNow)
        {
            _db.PhoneVerifications.Remove(pending);
            await _db.SaveChangesAsync(ct);
            return Result<AuthResponseDto>.Failure("The code has expired. Request a new one.", 400);
        }

        if (pending.Attempts >= _otpOptions.MaxAttempts)
            return Result<AuthResponseDto>.Failure("Too many incorrect attempts. Request a new code.", 429);

        if (!_passwordHasher.Verify(dto.Code, pending.CodeHash))
        {
            pending.Attempts++;
            await _db.SaveChangesAsync(ct);
            return Result<AuthResponseDto>.Failure("Incorrect code.", 400);
        }

        var alreadyRegistered = await _db.CustomerUsers
            .AnyAsync(u => u.MobileNumber == normalizedMobile, ct);
        if (alreadyRegistered)
        {
            _db.PhoneVerifications.Remove(pending);
            await _db.SaveChangesAsync(ct);
            return Result<AuthResponseDto>.Failure("Mobile number already registered.", 409);
        }

        var user = new CustomerUser
        {
            MobileNumber = normalizedMobile,
            PinHash = pending.PinHash,
            FirstName = pending.FirstName,
            LastName = pending.LastName
        };
        _db.CustomerUsers.Add(user);
        _db.PhoneVerifications.Remove(pending);
        await _db.SaveChangesAsync(ct);

        var displayName = string.IsNullOrWhiteSpace(user.FirstName)
            ? user.MobileNumber
            : $"{user.FirstName} {user.LastName}".Trim();

        return Result<AuthResponseDto>.Success(
            await IssueTokenPairAsync(user.Id, user.MobileNumber, RefreshTokenUserType.Customer,
                "customer", displayName, role: null, ip, ct), 201);
    }

    public async Task<Result<AuthResponseDto>> LoginCustomerAsync(
        CustomerLoginDto dto, string? ip = null, CancellationToken ct = default)
    {
        var normalizedMobile = NormalizeMobile(dto.MobileNumber);
        var user = await _db.CustomerUsers
            .FirstOrDefaultAsync(u => u.MobileNumber == normalizedMobile, ct);

        if (user == null || !user.IsActive)
            return Result<AuthResponseDto>.Failure("Invalid credentials.", 401);

        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
        {
            var minutesRemaining = (int)Math.Ceiling((user.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes);
            return Result<AuthResponseDto>.Locked(minutesRemaining);
        }

        if (!_passwordHasher.Verify(dto.Pin4, user.PinHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.LockoutEnd = DateTime.UtcNow.Add(LockoutDuration);
                user.FailedLoginAttempts = 0;
            }
            await _db.SaveChangesAsync(ct);
            return Result<AuthResponseDto>.Failure("Invalid credentials.", 401);
        }

        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        await _db.SaveChangesAsync(ct);

        var displayName = string.IsNullOrWhiteSpace(user.FirstName)
            ? user.MobileNumber
            : $"{user.FirstName} {user.LastName}".Trim();

        return Result<AuthResponseDto>.Success(
            await IssueTokenPairAsync(user.Id, user.MobileNumber, RefreshTokenUserType.Customer,
                "customer", displayName, role: null, ip, ct));
    }

    public async Task<Result<AuthResponseDto>> LoginAdminAsync(
        AdminLoginDto dto, string? ip = null, CancellationToken ct = default)
    {
        var admin = await _db.AdminUsers
            .FirstOrDefaultAsync(u => u.Username == dto.Username && u.IsActive, ct);

        if (admin == null || !_passwordHasher.Verify(dto.Password, admin.PasswordHash))
            return Result<AuthResponseDto>.Failure("Invalid credentials.", 401);

        var role = admin.Role.ToString();
        var accessToken = _tokenService.GenerateAdminToken(admin.Id, admin.Username, role, admin.MustChangePassword);
        var refreshToken = await CreateRefreshTokenAsync(admin.Id, RefreshTokenUserType.Admin, ip, ct);

        return Result<AuthResponseDto>.Success(
            new AuthResponseDto(accessToken, refreshToken, "admin", admin.Id, admin.Username, role, admin.MustChangePassword));
    }

    public async Task<Result<AuthResponseDto>> RefreshAsync(
        string refreshTokenValue, string? ip = null, CancellationToken ct = default)
    {
        var storedToken = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == refreshTokenValue, ct);

        if (storedToken == null || storedToken.ExpiresAt <= DateTime.UtcNow)
            return Result<AuthResponseDto>.Failure("Invalid or expired refresh token.", 401);

        if (storedToken.IsRevoked)
        {
            // Reuse of a revoked token — possible token theft; revoke entire family
            _logger.LogWarning("Refresh token reuse detected for user {UserId}. Revoking all tokens.", storedToken.UserId);
            var activeTokens = await _db.RefreshTokens
                .Where(t => t.UserId == storedToken.UserId && !t.IsRevoked)
                .ToListAsync(ct);
            foreach (var t in activeTokens)
            {
                t.IsRevoked = true;
                t.RevokedAt = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync(ct);
            return Result<AuthResponseDto>.Failure("Invalid or expired refresh token.", 401);
        }

        // Revoke old token and chain it
        var newRefreshValue = _tokenService.GenerateRefreshToken();
        storedToken.IsRevoked = true;
        storedToken.RevokedAt = DateTime.UtcNow;
        storedToken.ReplacedByToken = newRefreshValue;

        var newRefreshToken = new RefreshToken
        {
            Token = newRefreshValue,
            UserType = storedToken.UserType,
            UserId = storedToken.UserId,
            ExpiresAt = DateTime.UtcNow.AddDays(_tokenService.RefreshTokenDays),
            CreatedByIp = ip
        };
        _db.RefreshTokens.Add(newRefreshToken);

        string accessToken;
        string displayName;
        string? role = null;

        if (storedToken.UserType == RefreshTokenUserType.Customer)
        {
            var user = await _db.CustomerUsers.FindAsync([storedToken.UserId], ct);
            if (user == null || !user.IsActive)
                return Result<AuthResponseDto>.Failure("User not found.", 401);

            displayName = string.IsNullOrWhiteSpace(user.FirstName)
                ? user.MobileNumber
                : $"{user.FirstName} {user.LastName}".Trim();
            accessToken = _tokenService.GenerateCustomerToken(user.Id, user.MobileNumber);

            await _db.SaveChangesAsync(ct);
            return Result<AuthResponseDto>.Success(
                new AuthResponseDto(accessToken, newRefreshValue, "customer", user.Id, displayName));
        }
        else
        {
            var admin = await _db.AdminUsers.FindAsync([storedToken.UserId], ct);
            if (admin == null || !admin.IsActive)
                return Result<AuthResponseDto>.Failure("User not found.", 401);

            role = admin.Role.ToString();
            accessToken = _tokenService.GenerateAdminToken(admin.Id, admin.Username, role, admin.MustChangePassword);

            await _db.SaveChangesAsync(ct);
            return Result<AuthResponseDto>.Success(
                new AuthResponseDto(accessToken, newRefreshValue, "admin", admin.Id, admin.Username, role, admin.MustChangePassword));
        }
    }

    public async Task RevokeAsync(string refreshTokenValue, string? ip = null, CancellationToken ct = default)
    {
        var storedToken = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == refreshTokenValue, ct);

        if (storedToken == null || storedToken.IsRevoked)
            return;

        storedToken.IsRevoked = true;
        storedToken.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Result<AuthResponseDto>> RefreshCustomerTokenAsync(
        Guid userId, CancellationToken ct = default)
    {
        var user = await _db.CustomerUsers.FindAsync([userId], ct);
        if (user == null)
            return Result<AuthResponseDto>.NotFound("User not found.");

        var displayName = string.IsNullOrWhiteSpace(user.FirstName)
            ? user.MobileNumber
            : $"{user.FirstName} {user.LastName}".Trim();

        return Result<AuthResponseDto>.Success(
            await IssueTokenPairAsync(user.Id, user.MobileNumber, RefreshTokenUserType.Customer,
                "customer", displayName, role: null, ip: null, ct));
    }

    public async Task<Result<CustomerProfileDto>> UpdateProfileAsync(
        Guid userId, UpdateProfileDto dto, CancellationToken ct = default)
    {
        var user = await _db.CustomerUsers.FindAsync([userId], ct);
        if (user == null)
            return Result<CustomerProfileDto>.NotFound("User not found.");

        if (dto.DateOfBirth.HasValue && dto.DateOfBirth.Value > DateOnly.FromDateTime(DateTime.UtcNow))
            return Result<CustomerProfileDto>.Failure("Date of birth cannot be in the future.", 422);

        user.FirstName = dto.FirstName.Trim();
        user.LastName = dto.LastName.Trim();
        user.DateOfBirth = dto.DateOfBirth;
        await _db.SaveChangesAsync(ct);

        return Result<CustomerProfileDto>.Success(
            new CustomerProfileDto(user.Id, user.MobileNumber, user.FirstName, user.LastName, user.CreatedAt, user.DateOfBirth));
    }

    /// <summary>
    /// Step 1 of changing an existing customer's mobile number: proves the customer
    /// actually controls the new number before it's applied, the same way registration
    /// proves it for a brand-new signup. Mirrors <see cref="StartRegistrationAsync"/>,
    /// but keyed by <see cref="VerificationPurpose.PhoneChange"/> and tied to this
    /// customer's id so it can't collide with someone else's in-flight registration or
    /// phone-change to the same destination number.
    /// </summary>
    public async Task<Result<RegistrationStartResponseDto>> StartPhoneChangeAsync(
        Guid userId, ChangeMobileNumberDto dto, CancellationToken ct = default)
    {
        var user = await _db.CustomerUsers.FindAsync([userId], ct);
        if (user == null)
            return Result<RegistrationStartResponseDto>.NotFound("User not found.");

        var normalizedMobile = NormalizeMobile(dto.MobileNumber);

        var alreadyTaken = await _db.CustomerUsers
            .AnyAsync(u => u.MobileNumber == normalizedMobile && u.Id != userId, ct);
        if (alreadyTaken)
            return Result<RegistrationStartResponseDto>.Failure("Mobile number already in use.", 409);

        var now = DateTime.UtcNow;
        var pending = await _db.PhoneVerifications
            .FirstOrDefaultAsync(v => v.MobileNumber == normalizedMobile
                && v.Purpose == VerificationPurpose.PhoneChange, ct);

        if (pending != null &&
            pending.LastSentAt.AddSeconds(_otpOptions.ResendCooldownSeconds) > now)
        {
            var wait = (int)Math.Ceiling(
                (pending.LastSentAt.AddSeconds(_otpOptions.ResendCooldownSeconds) - now).TotalSeconds);
            return Result<RegistrationStartResponseDto>.Failure(
                $"Please wait {wait}s before requesting another code.", 429);
        }

        var code = Random.Shared.Next(0, 10000).ToString("D4");
        var expiresAt = now.AddMinutes(_otpOptions.CodeExpiryMinutes);

        if (pending == null)
        {
            pending = new PhoneVerification
            {
                MobileNumber = normalizedMobile,
                Purpose = VerificationPurpose.PhoneChange,
            };
            _db.PhoneVerifications.Add(pending);
        }

        pending.CodeHash = _passwordHasher.Hash(code);
        pending.ExpiresAt = expiresAt;
        pending.LastSentAt = now;
        pending.Attempts = 0;
        pending.CustomerUserId = userId;

        await _db.SaveChangesAsync(ct);

        if (_otpOptions.Enabled)
        {
            try
            {
                await _otpSender.SendAsync(normalizedMobile, code, ct);
            }
            catch (OtpSendException ex)
            {
                _logger.LogError(ex, "Failed to send phone-change OTP to {Mobile}", normalizedMobile);
                return Result<RegistrationStartResponseDto>.Failure(
                    "Could not send the verification code. Please try again.", 502);
            }
        }
        else
        {
            _logger.LogWarning(
                "OTP delivery disabled — phone-change verification code for {Mobile} is {Code}",
                normalizedMobile, code);
        }

        return Result<RegistrationStartResponseDto>.Success(
            new RegistrationStartResponseDto(
                normalizedMobile, expiresAt, _otpOptions.Enabled ? null : code));
    }

    /// <summary>Step 2: confirm the code and apply the new mobile number.</summary>
    public async Task<Result<CustomerProfileDto>> VerifyPhoneChangeAsync(
        Guid userId, RegisterVerifyDto dto, CancellationToken ct = default)
    {
        var normalizedMobile = NormalizeMobile(dto.MobileNumber);

        var pending = await _db.PhoneVerifications
            .FirstOrDefaultAsync(v => v.MobileNumber == normalizedMobile
                && v.Purpose == VerificationPurpose.PhoneChange, ct);

        if (pending == null || pending.CustomerUserId != userId)
            return Result<CustomerProfileDto>.Failure("No pending verification. Start again.", 400);

        if (pending.ExpiresAt <= DateTime.UtcNow)
        {
            _db.PhoneVerifications.Remove(pending);
            await _db.SaveChangesAsync(ct);
            return Result<CustomerProfileDto>.Failure("The code has expired. Request a new one.", 400);
        }

        if (pending.Attempts >= _otpOptions.MaxAttempts)
            return Result<CustomerProfileDto>.Failure("Too many incorrect attempts. Request a new code.", 429);

        if (!_passwordHasher.Verify(dto.Code, pending.CodeHash))
        {
            pending.Attempts++;
            await _db.SaveChangesAsync(ct);
            return Result<CustomerProfileDto>.Failure("Incorrect code.", 400);
        }

        var user = await _db.CustomerUsers.FindAsync([userId], ct);
        if (user == null)
        {
            _db.PhoneVerifications.Remove(pending);
            await _db.SaveChangesAsync(ct);
            return Result<CustomerProfileDto>.NotFound("User not found.");
        }

        // Race guard — someone else could have claimed the number between start and verify.
        var stillFree = !await _db.CustomerUsers
            .AnyAsync(u => u.MobileNumber == normalizedMobile && u.Id != userId, ct);
        if (!stillFree)
        {
            _db.PhoneVerifications.Remove(pending);
            await _db.SaveChangesAsync(ct);
            return Result<CustomerProfileDto>.Failure("Mobile number already in use.", 409);
        }

        user.MobileNumber = normalizedMobile;
        user.UpdatedAt = DateTime.UtcNow;
        _db.PhoneVerifications.Remove(pending);
        await _db.SaveChangesAsync(ct);

        return Result<CustomerProfileDto>.Success(
            new CustomerProfileDto(user.Id, user.MobileNumber, user.FirstName, user.LastName, user.CreatedAt, user.DateOfBirth));
    }

    public async Task<Result<bool>> ChangeAdminPasswordAsync(
        Guid adminId, ChangeAdminPasswordDto dto, CancellationToken ct = default)
    {
        var admin = await _db.AdminUsers.FindAsync([adminId], ct);
        if (admin == null)
            return Result<bool>.NotFound("Admin not found.");

        if (!_passwordHasher.Verify(dto.CurrentPassword, admin.PasswordHash))
            return Result<bool>.Failure("Current password is incorrect.", 400);

        admin.PasswordHash = _passwordHasher.Hash(dto.NewPassword);
        admin.MustChangePassword = false;
        await _db.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }

    private async Task<AuthResponseDto> IssueTokenPairAsync(
        Guid userId, string mobileNumber, RefreshTokenUserType userType,
        string userTypeStr, string displayName, string? role,
        string? ip, CancellationToken ct)
    {
        var accessToken = _tokenService.GenerateCustomerToken(userId, mobileNumber);
        var refreshToken = await CreateRefreshTokenAsync(userId, userType, ip, ct);
        return new AuthResponseDto(accessToken, refreshToken, userTypeStr, userId, displayName, role);
    }

    private async Task<string> CreateRefreshTokenAsync(
        Guid userId, RefreshTokenUserType userType, string? ip, CancellationToken ct)
    {
        var tokenValue = _tokenService.GenerateRefreshToken();
        var days = _tokenService.RefreshTokenDays;

        _db.RefreshTokens.Add(new RefreshToken
        {
            Token = tokenValue,
            UserType = userType,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(days),
            CreatedByIp = ip
        });
        await _db.SaveChangesAsync(ct);
        return tokenValue;
    }

    private static string NormalizeMobile(string mobile)
        => mobile.Trim().Replace(" ", "").Replace("-", "");
}
