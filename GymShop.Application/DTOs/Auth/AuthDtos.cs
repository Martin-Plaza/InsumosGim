using System.ComponentModel.DataAnnotations;
using GymShop.Application.Common;

namespace GymShop.Application.DTOs.Auth;

public record RegisterRequest(
    [Required, StringLength(ValidationLimits.UserName)] string Name,
    [Required, StringLength(ValidationLimits.UserName)] string LastName,
    [Required, EmailAddress, StringLength(ValidationLimits.Email)] string Email,
    [Required, StrongPassword] string Password);

public record RegistrationPendingResponse(string Email, int ExpiresInSeconds, string? DevelopmentCode);

public record VerifyEmailRequest(
    [Required, EmailAddress, StringLength(ValidationLimits.Email)] string Email,
    [Required, RegularExpression("^[0-9]{6}$")] string Code);

public record ResendVerificationRequest(
    [Required, EmailAddress, StringLength(ValidationLimits.Email)] string Email);

public record GoogleLoginRequest([Required] string Credential);

public record LoginRequest(
    [Required, EmailAddress, StringLength(ValidationLimits.Email)] string Email,
    [Required] string Password);

public record RequestPasswordResetRequest(
    [Required, EmailAddress, StringLength(ValidationLimits.Email)] string Email);

public record PasswordResetPendingResponse(string Message, int ExpiresInSeconds, string? DevelopmentCode);

public record ConfirmPasswordResetRequest(
    [Required, EmailAddress, StringLength(ValidationLimits.Email)] string Email,
    [Required, RegularExpression("^[0-9]{6}$")] string Code,
    [Required, StrongPassword] string NewPassword);

public record PasswordResetCompletedResponse(string Message);

public record AuthResponse(string Token, UserResponse User);

public record UserResponse(int Id, string Email, string Name, string? LastName, string Role);

