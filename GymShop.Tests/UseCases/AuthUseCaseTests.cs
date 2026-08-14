using GymShop.Application.Abstractions;
using GymShop.Application.Common;
using GymShop.Application.DTOs.Auth;
using GymShop.Application.UseCases.Auth;
using GymShop.Domain.Entities;
using GymShop.Infrastructure.Services;
using GymShop.Tests.TestSupport;

namespace GymShop.Tests.UseCases;

public class AuthUseCaseTests
{
    [Fact]
    public async Task Register_creates_unverified_user_and_sends_code()
    {
        await using var db = await TestDbContextFactory.CreateAsync(); var sender = new FakeSender();
        var result = await Register(db, sender).ExecuteAsync(new RegisterRequest("Cliente", "Test", "CLIENTE@TEST.COM", "clave123"));
        Assert.True(result.IsSuccess); Assert.Equal("cliente@test.com", result.Value!.Email); Assert.Equal(60, result.Value.ExpiresInSeconds);
        Assert.Single(db.Users); Assert.Null(db.Users.Single().EmailVerifiedAt); Assert.Single(db.EmailVerificationCodes); Assert.NotNull(sender.Code);
    }

    [Fact]
    public async Task Verify_valid_code_marks_email_and_returns_token()
    {
        await using var db = await TestDbContextFactory.CreateAsync(); var sender = new FakeSender();
        await Register(db, sender).ExecuteAsync(new RegisterRequest("Cliente", "Test", "cliente@test.com", "clave123"));
        var result = await new VerifyEmailUseCase(db, new FakeJwtTokenService(), TimeProvider.System).ExecuteAsync(new VerifyEmailRequest("cliente@test.com", sender.Code!));
        Assert.True(result.IsSuccess); Assert.StartsWith("test-token-for-", result.Value!.Token); Assert.NotNull(db.Users.Single().EmailVerifiedAt); Assert.NotNull(db.EmailVerificationCodes.Single().ConsumedAtUtc);
    }

    [Fact]
    public async Task Verify_rejects_invalid_code_and_counts_attempt()
    {
        await using var db = await TestDbContextFactory.CreateAsync(); var sender = new FakeSender();
        await Register(db, sender).ExecuteAsync(new RegisterRequest("Cliente", "Test", "cliente@test.com", "clave123"));
        var result = await new VerifyEmailUseCase(db, new FakeJwtTokenService(), TimeProvider.System).ExecuteAsync(new VerifyEmailRequest("cliente@test.com", "999999"));
        Assert.False(result.IsSuccess); Assert.Equal("El codigo es incorrecto.", result.Error!.Message); Assert.Equal(1, db.EmailVerificationCodes.Single().FailedAttempts);
    }

    [Fact]
    public async Task Verify_rejects_expired_code()
    {
        await using var db = await TestDbContextFactory.CreateAsync(); var sender = new FakeSender();
        await Register(db, sender).ExecuteAsync(new RegisterRequest("Cliente", "Test", "cliente@test.com", "clave123"));
        db.EmailVerificationCodes.Single().ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1); await db.SaveChangesAsync();
        var result = await new VerifyEmailUseCase(db, new FakeJwtTokenService(), TimeProvider.System).ExecuteAsync(new VerifyEmailRequest("cliente@test.com", sender.Code!));
        Assert.False(result.IsSuccess); Assert.Equal("El codigo vencio. Solicita uno nuevo.", result.Error!.Message); Assert.Null(db.Users.Single().EmailVerifiedAt);
    }

    [Fact]
    public async Task Resend_is_blocked_while_current_code_is_valid()
    {
        await using var db = await TestDbContextFactory.CreateAsync(); var sender = new FakeSender();
        await Register(db, sender).ExecuteAsync(new RegisterRequest("Cliente", "Test", "cliente@test.com", "clave123"));
        var result = await new ResendVerificationUseCase(db, sender, TimeProvider.System).ExecuteAsync(new ResendVerificationRequest("cliente@test.com"));
        Assert.False(result.IsSuccess); Assert.Equal(AppErrorType.Conflict, result.Error!.Type); Assert.Single(db.EmailVerificationCodes);
    }

    [Fact]
    public async Task Login_rejects_user_until_email_is_verified()
    {
        await using var db = await TestDbContextFactory.CreateAsync(); var sender = new FakeSender(); var hasher = new PasswordHasher();
        await new RegisterUserUseCase(db, hasher, sender, TimeProvider.System).ExecuteAsync(new RegisterRequest("Cliente", "Test", "cliente@test.com", "clave123"));
        var result = await new LoginUserUseCase(db, hasher, new FakeJwtTokenService()).ExecuteAsync(new LoginRequest("cliente@test.com", "clave123"));
        Assert.False(result.IsSuccess); Assert.Equal(AppErrorType.Unauthorized, result.Error!.Type);
    }

    [Fact]
    public async Task Login_succeeds_after_verification()
    {
        await using var db = await TestDbContextFactory.CreateAsync(); var sender = new FakeSender(); var hasher = new PasswordHasher();
        await new RegisterUserUseCase(db, hasher, sender, TimeProvider.System).ExecuteAsync(new RegisterRequest("Cliente", "Test", "cliente@test.com", "clave123"));
        await new VerifyEmailUseCase(db, new FakeJwtTokenService(), TimeProvider.System).ExecuteAsync(new VerifyEmailRequest("cliente@test.com", sender.Code!));
        var result = await new LoginUserUseCase(db, hasher, new FakeJwtTokenService()).ExecuteAsync(new LoginRequest("cliente@test.com", "clave123"));
        Assert.True(result.IsSuccess); Assert.Equal("Test", result.Value!.User.LastName);
    }

    [Fact]
    public async Task Register_rejects_duplicate_email_case_insensitively()
    {
        await using var db = await TestDbContextFactory.CreateAsync(); var sender = new FakeSender(); var register = Register(db, sender);
        await register.ExecuteAsync(new RegisterRequest("Cliente", "Test", "Cliente@Test.com", "clave123"));
        var result = await register.ExecuteAsync(new RegisterRequest("Otro", "Test", "cliente@test.com", "clave123"));
        Assert.False(result.IsSuccess); Assert.Equal(AppErrorType.Conflict, result.Error!.Type);
    }

    [Fact]
    public async Task Google_login_links_verified_identity_to_existing_manual_email()
    {
        await using var db = await TestDbContextFactory.CreateAsync(); var sender = new FakeSender();
        await Register(db, sender).ExecuteAsync(new RegisterRequest("Cliente", "Manual", "cliente@test.com", "clave123"));
        var verifier = new FakeExternalVerifier(new ExternalIdentity("Google", "google-sub-1", "CLIENTE@TEST.COM", true, "Cliente", "Google"));
        var result = await new GoogleLoginUseCase(db, verifier, new FakeJwtTokenService(), TimeProvider.System).ExecuteAsync(new GoogleLoginRequest("credential"));
        Assert.True(result.IsSuccess); Assert.Single(db.Users); Assert.Single(db.UserExternalLogins); Assert.NotNull(db.Users.Single().EmailVerifiedAt);
    }

    [Fact]
    public async Task Google_login_rejects_unverified_email()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var verifier = new FakeExternalVerifier(new ExternalIdentity("Google", "google-sub-2", "unsafe@test.com", false, "Unsafe", null));
        var result = await new GoogleLoginUseCase(db, verifier, new FakeJwtTokenService(), TimeProvider.System).ExecuteAsync(new GoogleLoginRequest("credential"));
        Assert.False(result.IsSuccess); Assert.Equal(AppErrorType.Unauthorized, result.Error!.Type); Assert.Empty(db.Users);
    }

    [Fact]
    public async Task Password_reset_request_is_generic_for_existing_and_unknown_email()
    {
        await using var db = await TestDbContextFactory.CreateAsync(); var sender = new FakePasswordResetSender(); var hasher = new PasswordHasher();
        var user = await SeedVerifiedUserAsync(db, hasher, "known@test.com", "clave123");
        var useCase = new RequestPasswordResetUseCase(db, sender, hasher, TimeProvider.System);

        var known = await useCase.ExecuteAsync(new RequestPasswordResetRequest("KNOWN@TEST.COM"));
        var knownCode = sender.Code;
        var unknown = await useCase.ExecuteAsync(new RequestPasswordResetRequest("unknown@test.com"));

        Assert.True(known.IsSuccess); Assert.True(unknown.IsSuccess);
        Assert.Equal(known.Value!.Message, unknown.Value!.Message);
        Assert.Equal(600, known.Value.ExpiresInSeconds); Assert.Equal(600, unknown.Value.ExpiresInSeconds);
        Assert.Matches("^[0-9]{6}$", knownCode!); Assert.Matches("^[0-9]{6}$", sender.Code!);
        Assert.Single(db.PasswordResetCodes); Assert.Equal(user.Id, db.PasswordResetCodes.Single().UserId);
        Assert.DoesNotContain(knownCode!, db.PasswordResetCodes.Single().CodeHash);
    }

    [Fact]
    public async Task Password_reset_changes_password_consumes_code_and_increments_token_version()
    {
        await using var db = await TestDbContextFactory.CreateAsync(); var sender = new FakePasswordResetSender(); var hasher = new PasswordHasher();
        var user = await SeedVerifiedUserAsync(db, hasher, "reset@test.com", "clave123");
        await new RequestPasswordResetUseCase(db, sender, hasher, TimeProvider.System).ExecuteAsync(new RequestPasswordResetRequest(user.Email));
        var tokenVersion = user.TokenVersion;

        var result = await new ConfirmPasswordResetUseCase(db, hasher, TimeProvider.System).ExecuteAsync(new ConfirmPasswordResetRequest(user.Email, sender.Code!, "nuevaClave456"));

        Assert.True(result.IsSuccess); Assert.True(hasher.Verify("nuevaClave456", user.PasswordHash)); Assert.False(hasher.Verify("clave123", user.PasswordHash));
        Assert.Equal(tokenVersion + 1, user.TokenVersion); Assert.NotNull(db.PasswordResetCodes.Single().ConsumedAtUtc);
        var oldLogin = await new LoginUserUseCase(db, hasher, new FakeJwtTokenService()).ExecuteAsync(new LoginRequest(user.Email, "clave123"));
        var newLogin = await new LoginUserUseCase(db, hasher, new FakeJwtTokenService()).ExecuteAsync(new LoginRequest(user.Email, "nuevaClave456"));
        Assert.False(oldLogin.IsSuccess); Assert.True(newLogin.IsSuccess);
    }

    [Fact]
    public async Task Password_reset_code_is_single_use_and_resend_invalidates_previous_code()
    {
        await using var db = await TestDbContextFactory.CreateAsync(); var sender = new FakePasswordResetSender(); var hasher = new PasswordHasher();
        var user = await SeedVerifiedUserAsync(db, hasher, "single@test.com", "clave123");
        var request = new RequestPasswordResetUseCase(db, sender, hasher, TimeProvider.System);
        await request.ExecuteAsync(new RequestPasswordResetRequest(user.Email)); var firstCode = sender.Code!;
        await request.ExecuteAsync(new RequestPasswordResetRequest(user.Email)); var secondCode = sender.Code!;
        var confirm = new ConfirmPasswordResetUseCase(db, hasher, TimeProvider.System);

        Assert.False((await confirm.ExecuteAsync(new ConfirmPasswordResetRequest(user.Email, firstCode, "nuevaClave456"))).IsSuccess);
        Assert.True((await confirm.ExecuteAsync(new ConfirmPasswordResetRequest(user.Email, secondCode, "nuevaClave456"))).IsSuccess);
        Assert.False((await confirm.ExecuteAsync(new ConfirmPasswordResetRequest(user.Email, secondCode, "otraClave789"))).IsSuccess);
        Assert.Equal(2, db.PasswordResetCodes.Count()); Assert.All(db.PasswordResetCodes, code => Assert.NotNull(code.ConsumedAtUtc));
    }

    [Fact]
    public async Task Password_reset_rejects_expired_code_and_limits_failed_attempts()
    {
        await using var db = await TestDbContextFactory.CreateAsync(); var sender = new FakePasswordResetSender(); var hasher = new PasswordHasher();
        var user = await SeedVerifiedUserAsync(db, hasher, "attempts@test.com", "clave123");
        var request = new RequestPasswordResetUseCase(db, sender, hasher, TimeProvider.System);
        var confirm = new ConfirmPasswordResetUseCase(db, hasher, TimeProvider.System);
        await request.ExecuteAsync(new RequestPasswordResetRequest(user.Email));
        db.PasswordResetCodes.Single().ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1); await db.SaveChangesAsync();
        Assert.False((await confirm.ExecuteAsync(new ConfirmPasswordResetRequest(user.Email, sender.Code!, "nuevaClave456"))).IsSuccess);

        await request.ExecuteAsync(new RequestPasswordResetRequest(user.Email)); var validCode = sender.Code!;
        for (var attempt = 0; attempt < 5; attempt++) Assert.False((await confirm.ExecuteAsync(new ConfirmPasswordResetRequest(user.Email, "999999", "nuevaClave456"))).IsSuccess);
        Assert.False((await confirm.ExecuteAsync(new ConfirmPasswordResetRequest(user.Email, validCode, "nuevaClave456"))).IsSuccess);
        Assert.Equal(5, db.PasswordResetCodes.OrderByDescending(x => x.Id).First().FailedAttempts);
    }

    private static async Task<User> SeedVerifiedUserAsync(IApplicationDbContext db, PasswordHasher hasher, string email, string password)
    {
        var role = db.Roles.Single(x => x.Name == "User");
        var user = new User { Email = email, Name = "Reset", PasswordHash = hasher.Hash(password), RoleId = role.Id, Role = role, IsActive = true, EmailVerifiedAt = DateTime.UtcNow };
        db.Users.Add(user); await db.SaveChangesAsync(); return user;
    }

    private static RegisterUserUseCase Register(IApplicationDbContext db, FakeSender sender) => new(db, new PasswordHasher(), sender, TimeProvider.System);
    private sealed class FakeSender : IVerificationEmailSender { public string? Code { get; private set; } public Task<string?> SendAsync(string email, string code, CancellationToken cancellationToken = default) { Code = code; return Task.FromResult<string?>(code); } }
    private sealed class FakePasswordResetSender : IPasswordResetEmailSender { public string? Code { get; private set; } public Task<string?> SendAsync(string email, string code, CancellationToken cancellationToken = default) { Code = code; return Task.FromResult<string?>(code); } }
    private sealed class FakeExternalVerifier(ExternalIdentity? identity) : IExternalIdentityVerifier { public Task<ExternalIdentity?> VerifyGoogleAsync(string credential, CancellationToken cancellationToken = default) => Task.FromResult(identity); }
}
