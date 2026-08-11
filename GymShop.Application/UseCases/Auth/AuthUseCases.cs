using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using GymShop.Application.Abstractions;
using GymShop.Application.Common;
using GymShop.Application.DTOs.Auth;
using GymShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymShop.Application.UseCases.Auth;

public interface IRegisterUserUseCase { Task<AppResult<RegistrationPendingResponse>> ExecuteAsync(RegisterRequest request, CancellationToken cancellationToken = default); }
public interface IVerifyEmailUseCase { Task<AppResult<AuthResponse>> ExecuteAsync(VerifyEmailRequest request, CancellationToken cancellationToken = default); }
public interface IResendVerificationUseCase { Task<AppResult<RegistrationPendingResponse>> ExecuteAsync(ResendVerificationRequest request, CancellationToken cancellationToken = default); }
public interface IGoogleLoginUseCase { Task<AppResult<AuthResponse>> ExecuteAsync(GoogleLoginRequest request, CancellationToken cancellationToken = default); }
public interface ILoginUserUseCase { Task<AppResult<AuthResponse>> ExecuteAsync(LoginRequest request, CancellationToken cancellationToken = default); }
public interface IGetCurrentUserUseCase { Task<AppResult<UserResponse>> ExecuteAsync(int userId, CancellationToken cancellationToken = default); }

internal static class AuthMapping
{
    public static UserResponse User(User user) => new(user.Id, user.Email, user.Name, user.LastName, user.Role.Name);
    public static AuthResponse Auth(User user, IJwtTokenService jwt) => new(jwt.CreateToken(user), User(user));
}

public sealed class RegisterUserUseCase : IRegisterUserUseCase
{
    private readonly IApplicationDbContext _db; private readonly IPasswordHasher _hasher; private readonly IVerificationEmailSender _sender; private readonly TimeProvider _time;
    public RegisterUserUseCase(IApplicationDbContext db, IPasswordHasher hasher, IVerificationEmailSender sender, TimeProvider time) => (_db, _hasher, _sender, _time) = (db, hasher, sender, time);

    public async Task<AppResult<RegistrationPendingResponse>> ExecuteAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant(); var name = request.Name.Trim(); var lastName = request.LastName.Trim();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(lastName) || email.Length > ValidationLimits.Email || !new EmailAddressAttribute().IsValid(email))
            return AppResult<RegistrationPendingResponse>.Failure(AppErrorType.Validation, "Nombre, apellido y email valido son obligatorios.");
        if (!new StrongPasswordAttribute().IsValid(request.Password))
            return AppResult<RegistrationPendingResponse>.Failure(AppErrorType.Validation, "La password debe tener entre 8 y 128 caracteres e incluir al menos una letra y un numero.");
        if (await _db.Users.AnyAsync(x => x.Email == email, cancellationToken))
            return AppResult<RegistrationPendingResponse>.Failure(AppErrorType.Conflict, "El email ya esta registrado.");

        var role = await _db.Roles.SingleAsync(x => x.Name == "User", cancellationToken);
        var user = new User { Email = email, Name = name, LastName = lastName, PasswordHash = _hasher.Hash(request.Password), RoleId = role.Id, Role = role, IsActive = true };
        _db.Users.Add(user);
        return await Verification.CreateAsync(_db, _sender, _time, user, cancellationToken);
    }
}

internal static class Verification
{
    public const int LifetimeSeconds = 60;
    public static async Task<AppResult<RegistrationPendingResponse>> CreateAsync(IApplicationDbContext db, IVerificationEmailSender sender, TimeProvider time, User user, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var active = await db.EmailVerificationCodes.Where(x => x.UserId == user.Id && x.ConsumedAtUtc == null).ToListAsync(cancellationToken);
        foreach (var item in active) item.ConsumedAtUtc = now;
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        db.EmailVerificationCodes.Add(new EmailVerificationCode { User = user, UserId = user.Id, CodeHash = Hash(code), CreatedAtUtc = now, ExpiresAtUtc = now.AddSeconds(LifetimeSeconds) });
        await db.SaveChangesAsync(cancellationToken);
        var developmentCode = await sender.SendAsync(user.Email, code, cancellationToken);
        return AppResult<RegistrationPendingResponse>.Success(new(user.Email, LifetimeSeconds, developmentCode));
    }
    public static string Hash(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}

public sealed class VerifyEmailUseCase : IVerifyEmailUseCase
{
    private readonly IApplicationDbContext _db; private readonly IJwtTokenService _jwt; private readonly TimeProvider _time;
    public VerifyEmailUseCase(IApplicationDbContext db, IJwtTokenService jwt, TimeProvider time) => (_db, _jwt, _time) = (db, jwt, time);
    public async Task<AppResult<AuthResponse>> ExecuteAsync(VerifyEmailRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant(); var now = _time.GetUtcNow().UtcDateTime;
        var user = await _db.Users.Include(x => x.Role).Include(x => x.EmailVerificationCodes).SingleOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (user is null || user.EmailVerifiedAt is not null) return AppResult<AuthResponse>.Failure(AppErrorType.Validation, "No hay una verificacion pendiente para este email.");
        var verification = user.EmailVerificationCodes.Where(x => x.ConsumedAtUtc == null).OrderByDescending(x => x.CreatedAtUtc).FirstOrDefault();
        if (verification is null) return AppResult<AuthResponse>.Failure(AppErrorType.Validation, "No hay un codigo vigente. Solicita uno nuevo.");
        if (verification.ExpiresAtUtc <= now) return AppResult<AuthResponse>.Failure(AppErrorType.Validation, "El codigo vencio. Solicita uno nuevo.");
        if (verification.FailedAttempts >= 5) return AppResult<AuthResponse>.Failure(AppErrorType.Validation, "Superaste la cantidad de intentos. Solicita un codigo nuevo.");
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(verification.CodeHash), Convert.FromHexString(Verification.Hash(request.Code))))
        { verification.FailedAttempts++; await _db.SaveChangesAsync(cancellationToken); return AppResult<AuthResponse>.Failure(AppErrorType.Validation, "El codigo es incorrecto."); }
        verification.ConsumedAtUtc = now; user.EmailVerifiedAt = now; user.TokenVersion++;
        await _db.SaveChangesAsync(cancellationToken);
        return AppResult<AuthResponse>.Success(AuthMapping.Auth(user, _jwt));
    }
}

public sealed class ResendVerificationUseCase : IResendVerificationUseCase
{
    private readonly IApplicationDbContext _db; private readonly IVerificationEmailSender _sender; private readonly TimeProvider _time;
    public ResendVerificationUseCase(IApplicationDbContext db, IVerificationEmailSender sender, TimeProvider time) => (_db, _sender, _time) = (db, sender, time);
    public async Task<AppResult<RegistrationPendingResponse>> ExecuteAsync(ResendVerificationRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant(); var user = await _db.Users.SingleOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (user is null || user.EmailVerifiedAt is not null) return AppResult<RegistrationPendingResponse>.Failure(AppErrorType.Validation, "No se puede reenviar el codigo.");
        var now = _time.GetUtcNow().UtcDateTime;
        if (await _db.EmailVerificationCodes.AnyAsync(x => x.UserId == user.Id && x.ConsumedAtUtc == null && x.ExpiresAtUtc > now, cancellationToken))
            return AppResult<RegistrationPendingResponse>.Failure(AppErrorType.Conflict, "El codigo actual sigue vigente.");
        return await Verification.CreateAsync(_db, _sender, _time, user, cancellationToken);
    }
}

public sealed class GoogleLoginUseCase : IGoogleLoginUseCase
{
    private readonly IApplicationDbContext _db; private readonly IExternalIdentityVerifier _verifier; private readonly IJwtTokenService _jwt; private readonly TimeProvider _time;
    public GoogleLoginUseCase(IApplicationDbContext db, IExternalIdentityVerifier verifier, IJwtTokenService jwt, TimeProvider time) => (_db, _verifier, _jwt, _time) = (db, verifier, jwt, time);
    public async Task<AppResult<AuthResponse>> ExecuteAsync(GoogleLoginRequest request, CancellationToken cancellationToken = default)
    {
        var identity = await _verifier.VerifyGoogleAsync(request.Credential, cancellationToken);
        if (identity is null || !identity.EmailVerified) return AppResult<AuthResponse>.Failure(AppErrorType.Unauthorized, "La credencial de Google no es valida.");
        var external = await _db.UserExternalLogins.Include(x => x.User).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.Provider == identity.Provider && x.ProviderSubject == identity.Subject, cancellationToken);
        if (external is not null) return external.User.IsActive ? AppResult<AuthResponse>.Success(AuthMapping.Auth(external.User, _jwt)) : AppResult<AuthResponse>.Failure(AppErrorType.Unauthorized, "La cuenta no esta activa.");
        var email = identity.Email.Trim().ToLowerInvariant(); var user = await _db.Users.Include(x => x.Role).SingleOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (user is null)
        {
            var role = await _db.Roles.SingleAsync(x => x.Name == "User", cancellationToken);
            user = new User { Email = email, Name = identity.FirstName, LastName = identity.LastName, PasswordHash = string.Empty, Role = role, RoleId = role.Id, IsActive = true, EmailVerifiedAt = _time.GetUtcNow().UtcDateTime };
            _db.Users.Add(user);
        }
        else if (!user.IsActive) return AppResult<AuthResponse>.Failure(AppErrorType.Unauthorized, "La cuenta no esta activa.");
        else if (user.EmailVerifiedAt is null) user.EmailVerifiedAt = _time.GetUtcNow().UtcDateTime;
        _db.UserExternalLogins.Add(new UserExternalLogin { User = user, Provider = identity.Provider, ProviderSubject = identity.Subject, CreatedAtUtc = _time.GetUtcNow().UtcDateTime });
        await _db.SaveChangesAsync(cancellationToken);
        return AppResult<AuthResponse>.Success(AuthMapping.Auth(user, _jwt));
    }
}

public sealed class LoginUserUseCase : ILoginUserUseCase
{
    private readonly IApplicationDbContext _db; private readonly IPasswordHasher _hasher; private readonly IJwtTokenService _jwt;
    public LoginUserUseCase(IApplicationDbContext db, IPasswordHasher hasher, IJwtTokenService jwt) => (_db, _hasher, _jwt) = (db, hasher, jwt);
    public async Task<AppResult<AuthResponse>> ExecuteAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant(); var user = await _db.Users.Include(x => x.Role).SingleOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (user is null || !user.IsActive || user.EmailVerifiedAt is null || !_hasher.Verify(request.Password, user.PasswordHash)) return AppResult<AuthResponse>.Failure(AppErrorType.Unauthorized, "Credenciales invalidas o email sin verificar.");
        return AppResult<AuthResponse>.Success(AuthMapping.Auth(user, _jwt));
    }
}

public sealed class GetCurrentUserUseCase : IGetCurrentUserUseCase
{
    private readonly IApplicationDbContext _db; public GetCurrentUserUseCase(IApplicationDbContext db) => _db = db;
    public async Task<AppResult<UserResponse>> ExecuteAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.Include(x => x.Role).SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken);
        return user is null ? AppResult<UserResponse>.Failure(AppErrorType.NotFound, "Usuario no encontrado.") : AppResult<UserResponse>.Success(AuthMapping.User(user));
    }
}
