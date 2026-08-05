using System.ComponentModel.DataAnnotations;
using GymShop.Application.Common;

namespace GymShop.Application.DTOs.Auth;

public record RegisterRequest(
    [Required, StringLength(ValidationLimits.UserName)] string Name,
    [Required, EmailAddress, StringLength(ValidationLimits.Email)] string Email,
    [Required, StrongPassword] string Password);

public record LoginRequest(
    [Required, EmailAddress, StringLength(ValidationLimits.Email)] string Email,
    [Required] string Password);

public record AuthResponse(string Token, UserResponse User);

public record UserResponse(int Id, string Email, string Name, string Role);

