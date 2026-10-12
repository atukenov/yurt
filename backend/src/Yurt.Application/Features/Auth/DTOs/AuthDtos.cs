namespace Yurt.Application.Features.Auth.DTOs;

public record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    string UserType,
    Guid UserId,
    string DisplayName,
    string? Role = null,
    bool MustChangePassword = false);

public record RefreshRequestDto(string RefreshToken);

public record CustomerRegisterDto(string MobileNumber, string Pin4, string FirstName = "", string LastName = "");
public record RegisterVerifyDto(string MobileNumber, string Code);
/// <summary>
/// Result of starting registration / a phone change. When <see cref="VerificationRequired"/> is
/// false (OTP switched off) there is no second step: for registration <see cref="Auth"/> already
/// holds the issued tokens, and for a phone change the number has already been updated.
/// </summary>
public record RegistrationStartResponseDto(
    string MobileNumber,
    DateTime ExpiresAt,
    string? DevCode = null,
    bool VerificationRequired = true,
    AuthResponseDto? Auth = null);
public record CustomerLoginDto(string MobileNumber, string Pin4);
public record AdminLoginDto(string Username, string Password);
public record UpdateProfileDto(string FirstName, string LastName, DateOnly? DateOfBirth = null);
public record ChangeMobileNumberDto(string MobileNumber);

public record CustomerProfileDto(Guid Id, string MobileNumber, string FirstName, string LastName, DateTime CreatedAt, DateOnly? DateOfBirth = null);
public record ChangePinDto(string CurrentPin, string NewPin);
public record ChangeAdminPasswordDto(string CurrentPassword, string NewPassword);
public record RegisterDeviceTokenDto(string Token, string Platform);
