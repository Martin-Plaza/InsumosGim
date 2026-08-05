using GymShop.Application.Abstractions;
using GymShop.Application.Common;
using GymShop.Application.DTOs.Auth;
using GymShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymShop.Application.UseCases.Auth;

public interface IRegisterUserUseCase
{
    Task<AppResult<AuthResponse>> ExecuteAsync(RegisterRequest request, CancellationToken cancellationToken = default);
}

public interface ILoginUserUseCase
{
    Task<AppResult<AuthResponse>> ExecuteAsync(LoginRequest request, CancellationToken cancellationToken = default);
}

public interface IGetCurrentUserUseCase
{
    Task<AppResult<UserResponse>> ExecuteAsync(int userId, CancellationToken cancellationToken = default);
}

public class RegisterUserUseCase : IRegisterUserUseCase
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public RegisterUserUseCase(IApplicationDbContext db, IPasswordHasher passwordHasher, IJwtTokenService jwtTokenService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<AppResult<AuthResponse>> ExecuteAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return AppResult<AuthResponse>.Failure(AppErrorType.Validation, "Nombre, email y password son obligatorios.");
        }

        if (name.Length > ValidationLimits.UserName)
        {
            return AppResult<AuthResponse>.Failure(AppErrorType.Validation, "El nombre no puede superar los 100 caracteres.");
        }

        if (email.Length > ValidationLimits.Email || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
        {
            return AppResult<AuthResponse>.Failure(AppErrorType.Validation, "El email no es valido o supera los 256 caracteres.");
        }

        if (!new StrongPasswordAttribute().IsValid(request.Password))
        {
            return AppResult<AuthResponse>.Failure(AppErrorType.Validation, "La password debe tener entre 8 y 128 caracteres e incluir al menos una letra y un numero.");
        }

        if (await _db.Users.AnyAsync(x => x.Email == email, cancellationToken))
        {
            return AppResult<AuthResponse>.Failure(AppErrorType.Conflict, "El email ya esta registrado.");
        }

        var role = await _db.Roles.SingleAsync(x => x.Name == "User", cancellationToken);
        var user = new User
        {
            Email = email,
            Name = name,
            PasswordHash = _passwordHasher.Hash(request.Password),
            RoleId = role.Id,
            Role = role,
            IsActive = true
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        return AppResult<AuthResponse>.Success(CreateAuthResponse(user, _jwtTokenService));
    }

    private static AuthResponse CreateAuthResponse(User user, IJwtTokenService jwtTokenService)
    {
        return new AuthResponse(jwtTokenService.CreateToken(user), ToUserResponse(user));
    }

    private static UserResponse ToUserResponse(User user)
    {
        return new UserResponse(user.Id, user.Email, user.Name, user.Role.Name);
    }
}

public class LoginUserUseCase : ILoginUserUseCase
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public LoginUserUseCase(IApplicationDbContext db, IPasswordHasher passwordHasher, IJwtTokenService jwtTokenService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<AppResult<AuthResponse>> ExecuteAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users
            .Include(x => x.Role)
            .SingleOrDefaultAsync(x => x.Email == email, cancellationToken);

        if (user is null || !user.IsActive || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return AppResult<AuthResponse>.Failure(AppErrorType.Unauthorized, "Credenciales invalidas.");
        }

        return AppResult<AuthResponse>.Success(new AuthResponse(_jwtTokenService.CreateToken(user), ToUserResponse(user)));
    }

    private static UserResponse ToUserResponse(User user)
    {
        return new UserResponse(user.Id, user.Email, user.Name, user.Role.Name);
    }
}

public class GetCurrentUserUseCase : IGetCurrentUserUseCase
{
    private readonly IApplicationDbContext _db;

    public GetCurrentUserUseCase(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AppResult<UserResponse>> ExecuteAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .Include(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken);

        if (user is null)
        {
            return AppResult<UserResponse>.Failure(AppErrorType.NotFound, "Usuario no encontrado.");
        }

        return AppResult<UserResponse>.Success(new UserResponse(user.Id, user.Email, user.Name, user.Role.Name));
    }
}
