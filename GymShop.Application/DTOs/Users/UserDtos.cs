namespace GymShop.Application.DTOs.Users;

public record AdminUserResponse(
    int Id,
    string Email,
    string Name,
    string Role,
    bool IsActive,
    DateTime CreatedAt
);

public record CreateUserRequest(
    string Name,
    string Email,
    string Password,
    string Role
);

public record UpdateUserRoleRequest(string Role);

public record UpdateUserStatusRequest(bool IsActive);

