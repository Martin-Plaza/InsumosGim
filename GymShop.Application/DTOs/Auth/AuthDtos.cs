namespace GymShop.Application.DTOs.Auth;

public record RegisterRequest(string Name, string Email, string Password);

public record LoginRequest(string Email, string Password);

public record AuthResponse(string Token, UserResponse User);

public record UserResponse(int Id, string Email, string Name, string Role);

