using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Microsoft.EntityFrameworkCore;
using Yurt.Application.Features.Auth.DTOs;
using Yurt.Application.Features.Auth.Services;
using Yurt.Application.Features.Auth.Validators;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.WebApi.Common;
using Microsoft.AspNetCore.Authorization;

namespace Yurt.WebApi.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController : ApiControllerBase
{
    private readonly AuthService _authService;
    private readonly ICurrentUserService _currentUser;
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _hasher;

    public AuthController(
        AuthService authService,
        ICurrentUserService currentUser,
        IApplicationDbContext db,
        IPasswordHasher hasher)
    {
        _authService = authService;
        _currentUser = currentUser;
        _db = db;
        _hasher = hasher;
    }

    /// <summary>
    /// Start registration: sends a 4-digit verification code to the customer's mobile
    /// number over WhatsApp. Call <c>register/verify</c> with the code to finish.
    /// </summary>
    [HttpPost("register/start")]
    public async Task<IActionResult> RegisterStart(
        [FromBody] CustomerRegisterDto dto, CancellationToken ct)
    {
        var validator = new CustomerRegisterValidator();
        var validation = await validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return ValidationError(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var result = await _authService.StartRegistrationAsync(dto, ct);
        return ToResult(result);
    }

    /// <summary>Confirm the verification code and complete registration.</summary>
    [HttpPost("register/verify")]
    public async Task<IActionResult> RegisterVerify(
        [FromBody] RegisterVerifyDto dto, CancellationToken ct)
    {
        var validator = new RegisterVerifyValidator();
        var validation = await validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return ValidationError(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.VerifyRegistrationAsync(dto, ip, ct);
        return ToResult(result);
    }

    /// <summary>Login with mobile number and 4-digit PIN.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] CustomerLoginDto dto, CancellationToken ct)
    {
        var validator = new CustomerLoginValidator();
        var validation = await validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return ValidationError(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.LoginCustomerAsync(dto, ip, ct);
        return ToResult(result);
    }

    /// <summary>Rotate a refresh token — returns a new access token + refresh token pair.</summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequestDto dto, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.RefreshAsync(dto.RefreshToken, ip, ct);
        return ToResult(result);
    }

    /// <summary>Revoke a refresh token (logout).</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequestDto dto, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _authService.RevokeAsync(dto.RefreshToken, ip, ct);
        return NoContent();
    }

    /// <summary>Get current customer profile.</summary>
    [HttpGet("me")]
    [Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        var user = await _db.CustomerUsers.FindAsync([userId], ct);
        if (user == null) return NotFound();
        return Ok(new { user.Id, user.MobileNumber, user.FirstName, user.LastName, user.CreatedAt, user.DateOfBirth });
    }

    /// <summary>Update current customer's name.</summary>
    [HttpPut("me")]
    [Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileDto dto, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        var result = await _authService.UpdateProfileAsync(userId, dto, ct);
        return ToResult(result);
    }

    /// <summary>
    /// Step 1 of changing current customer's mobile number: sends a 4-digit verification
    /// code to the new number. Call <c>me/phone/verify</c> with the code to finish.
    /// </summary>
    [HttpPost("me/phone/start")]
    [Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> StartPhoneChange(
        [FromBody] ChangeMobileNumberDto dto, CancellationToken ct)
    {
        var validator = new ChangeMobileNumberValidator();
        var validation = await validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return ValidationError(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var userId = _currentUser.UserId!.Value;
        var result = await _authService.StartPhoneChangeAsync(userId, dto, ct);
        return ToResult(result);
    }

    /// <summary>Confirm the verification code and apply the new mobile number.</summary>
    [HttpPost("me/phone/verify")]
    [Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> VerifyPhoneChange(
        [FromBody] RegisterVerifyDto dto, CancellationToken ct)
    {
        var validator = new RegisterVerifyValidator();
        var validation = await validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return ValidationError(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var userId = _currentUser.UserId!.Value;
        var result = await _authService.VerifyPhoneChangeAsync(userId, dto, ct);
        return ToResult(result);
    }

    /// <summary>Change customer's 4-digit PIN.</summary>
    [HttpPut("pin")]
    [Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> ChangePin(
        [FromBody] ChangePinDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.CurrentPin) || dto.CurrentPin.Length != 4 ||
            string.IsNullOrWhiteSpace(dto.NewPin) || dto.NewPin.Length != 4)
            return ValidationError("Current PIN and new PIN must each be exactly 4 digits.");

        var userId = _currentUser.UserId!.Value;
        var user = await _db.CustomerUsers.FindAsync([userId], ct);
        if (user == null) return NotFound();

        if (!_hasher.Verify(dto.CurrentPin, user.PinHash))
            return Unauthorized(new ProblemDetails { Title = "Current PIN is incorrect." });

        user.PinHash = _hasher.Hash(dto.NewPin);
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Delete current customer account (soft delete).</summary>
    [HttpDelete("me")]
    [Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> DeleteAccount(CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        var user = await _db.CustomerUsers.FindAsync([userId], ct);
        if (user == null) return NotFound();

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Register (or re-assign) a device's push token for the current customer.
    /// Upserts by token so re-registering the same device is idempotent, and so a device
    /// that logs out and back in as a different customer reassigns the token rather than
    /// duplicating it.</summary>
    [HttpPost("device-tokens")]
    [Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> RegisterDeviceToken(
        [FromBody] RegisterDeviceTokenDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Token) ||
            !Enum.TryParse<DevicePlatform>(dto.Platform, ignoreCase: true, out var platform))
            return ValidationError("A token and a valid platform (ios/android) are required.");

        var userId = _currentUser.UserId!.Value;
        var existing = await _db.CustomerDeviceTokens.FirstOrDefaultAsync(t => t.Token == dto.Token, ct);
        if (existing != null)
        {
            existing.CustomerUserId = userId;
            existing.Platform = platform;
            existing.LastSeenAt = DateTime.UtcNow;
        }
        else
        {
            _db.CustomerDeviceTokens.Add(new CustomerDeviceToken
            {
                CustomerUserId = userId,
                Token = dto.Token,
                Platform = platform,
            });
        }
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Remove a device's push token, e.g. on logout, so a signed-out device stops
    /// receiving another customer's pushes if someone else logs in on it.</summary>
    [HttpDelete("device-tokens/{token}")]
    [Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> UnregisterDeviceToken(string token, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        var existing = await _db.CustomerDeviceTokens
            .FirstOrDefaultAsync(t => t.Token == token && t.CustomerUserId == userId, ct);
        if (existing != null)
        {
            _db.CustomerDeviceTokens.Remove(existing);
            await _db.SaveChangesAsync(ct);
        }
        return NoContent();
    }

    /// <summary>Get lifetime order stats for the current customer, including the
    /// derived figures the achievements grid needs (streaks, timing, repeat items).</summary>
    [HttpGet("me/stats")]
    [Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        var orders = await _db.Orders
            .Where(o => o.CustomerUserId == userId && o.Status == OrderStatus.Completed)
            .Include(o => o.Items)
            .ToListAsync(ct);

        // All customers currently order from Kazakhstan locations (UTC+5, no DST) —
        // fixed offset avoids a timezone-database dependency for this.
        var almatyOffset = TimeSpan.FromHours(5);
        var localTimestamps = orders
            .Select(o => (o.CompletedAt ?? o.CreatedAt) + almatyOffset)
            .ToList();
        var localDates = localTimestamps.Select(t => t.Date).Distinct().OrderBy(d => d).ToList();

        var weekStarts = localDates.Select(MondayOf).Distinct().OrderBy(d => d).ToList();
        var weekendStarts = localDates
            .Where(d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            .Select(d => d.DayOfWeek == DayOfWeek.Sunday ? d.AddDays(-1) : d)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        var drinksByDate = orders
            .GroupBy(o => ((o.CompletedAt ?? o.CreatedAt) + almatyOffset).Date)
            .Select(g => g.Sum(o => o.Items.Sum(i => i.Quantity)));

        var repeatItemCounts = orders
            .SelectMany(o => o.Items)
            .GroupBy(i => (i.MenuItemId, i.VariantId))
            .Select(g => g.Sum(i => i.Quantity));

        return Ok(new
        {
            totalOrders = orders.Count,
            totalSpent = orders.Sum(o => o.Total),
            totalDrinks = orders.Sum(o => o.Items.Sum(i => i.Quantity)),
            distinctLocations = orders.Select(o => o.LocationId).Distinct().Count(),
            redeemedOrders = orders.Count(o => o.LoyaltyPointsSpent is > 0),
            earlyOrders = localTimestamps.Count(t => t.Hour < 9),
            lateOrders = localTimestamps.Count(t => t.Hour >= 21),
            maxDailyDrinks = drinksByDate.DefaultIfEmpty(0).Max(),
            maxDailyStreak = LongestRun(localDates, TimeSpan.FromDays(1)),
            maxWeeklyStreak = LongestRun(weekStarts, TimeSpan.FromDays(7)),
            maxWeekendStreak = LongestRun(weekendStarts, TimeSpan.FromDays(7)),
            maxRepeatItemCount = repeatItemCounts.DefaultIfEmpty(0).Max(),
        });
    }

    private static DateTime MondayOf(DateTime date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff);
    }

    /// <summary>Longest run of consecutive buckets (days/weeks) in a sorted, deduplicated
    /// date list — the customer's best-ever streak, not just the one active right now,
    /// since a badge earned this way should never be revoked by an idle day later.</summary>
    private static int LongestRun(IReadOnlyList<DateTime> sortedDates, TimeSpan step)
    {
        if (sortedDates.Count == 0) return 0;
        var best = 1;
        var current = 1;
        for (var i = 1; i < sortedDates.Count; i++)
        {
            if (sortedDates[i] - sortedDates[i - 1] == step) current++;
            else current = 1;
            if (current > best) best = current;
        }
        return best;
    }
}
