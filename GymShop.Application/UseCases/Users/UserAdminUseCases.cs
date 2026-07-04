using GymShop.Application.Abstractions;
using GymShop.Application.Common;
using GymShop.Application.DTOs.Users;
using GymShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymShop.Application.UseCases.Users;

public interface IGetUsersUseCase
{
    Task<List<AdminUserResponse>> ExecuteAsync(CancellationToken cancellationToken = default);
}

public interface ICreateUserUseCase
{
    Task<AppResult<AdminUserResponse>> ExecuteAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
}

public interface IUpdateUserRoleUseCase
{
    Task<AppResult> ExecuteAsync(int id, UpdateUserRoleRequest request, CancellationToken cancellationToken = default);
}

public interface IUpdateUserStatusUseCase
{
    Task<AppResult> ExecuteAsync(int id, UpdateUserStatusRequest request, CancellationToken cancellationToken = default);
}

public class GetUsersUseCase : IGetUsersUseCase
{
    private readonly IApplicationDbContext _db;

    public GetUsersUseCase(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<AdminUserResponse>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Users
            .AsNoTracking()
            .Include(x => x.Role)
            .OrderByDescending(x => x.Id)
            .Select(x => UserAdminMapper.ToResponse(x))
            .ToListAsync(cancellationToken);
    }
}

public class CreateUserUseCase : ICreateUserUseCase
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;

    public CreateUserUseCase(IApplicationDbContext db, IPasswordHasher passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    public async Task<AppResult<AdminUserResponse>> ExecuteAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return AppResult<AdminUserResponse>.Failure(AppErrorType.Validation, "Nombre, email y password son obligatorios.");
        }

        if (request.Password.Length < 6)
        {
            return AppResult<AdminUserResponse>.Failure(AppErrorType.Validation, "La password debe tener al menos 6 caracteres.");
        }

        if (await _db.Users.AnyAsync(x => x.Email == email, cancellationToken))
        {
            return AppResult<AdminUserResponse>.Failure(AppErrorType.Conflict, "El email ya esta registrado.");
        }

        var role = await UserRoleLookup.FindRoleAsync(_db, request.Role, cancellationToken);
        if (role is null)
        {
            return AppResult<AdminUserResponse>.Failure(AppErrorType.Validation, "Rol invalido.");
        }

        var user = new User
        {
            Name = name,
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            RoleId = role.Id,
            Role = role,
            IsActive = true
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        return AppResult<AdminUserResponse>.Success(UserAdminMapper.ToResponse(user));
    }
}

public class UpdateUserRoleUseCase : IUpdateUserRoleUseCase
{
    private readonly IApplicationDbContext _db;

    public UpdateUserRoleUseCase(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AppResult> ExecuteAsync(int id, UpdateUserRoleRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
        {
            return AppResult.Failure(AppErrorType.NotFound, "Usuario no encontrado.");
        }

        var role = await UserRoleLookup.FindRoleAsync(_db, request.Role, cancellationToken);
        if (role is null)
        {
            return AppResult.Failure(AppErrorType.Validation, "Rol invalido.");
        }

        user.RoleId = role.Id;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return AppResult.Success();
    }
}

public class UpdateUserStatusUseCase : IUpdateUserStatusUseCase
{
    private readonly IApplicationDbContext _db;

    public UpdateUserStatusUseCase(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AppResult> ExecuteAsync(int id, UpdateUserStatusRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
        {
            return AppResult.Failure(AppErrorType.NotFound, "Usuario no encontrado.");
        }

        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return AppResult.Success();
    }
}

internal static class UserRoleLookup
{
    public static async Task<Role?> FindRoleAsync(IApplicationDbContext db, string role, CancellationToken cancellationToken)
    {
        var normalized = NormalizeRole(role);
        return normalized is null
            ? null
            : await db.Roles.SingleOrDefaultAsync(x => x.Name == normalized, cancellationToken);
    }

    private static string? NormalizeRole(string role)
    {
        return role.Trim().ToLowerInvariant() switch
        {
            "user" => "User",
            "admin" => "Admin",
            "superadmin" or "super-admin" => "SuperAdmin",
            _ => null
        };
    }
}

internal static class UserAdminMapper
{
    public static AdminUserResponse ToResponse(User user)
    {
        return new AdminUserResponse(
            user.Id,
            user.Email,
            user.Name,
            user.Role.Name,
            user.IsActive,
            user.CreatedAt
        );
    }
}
