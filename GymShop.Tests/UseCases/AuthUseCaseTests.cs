using GymShop.Application.Abstractions;
using GymShop.Application.Common;
using GymShop.Application.DTOs.Auth;
using GymShop.Application.UseCases.Auth;
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

    private static RegisterUserUseCase Register(IApplicationDbContext db, FakeSender sender) => new(db, new PasswordHasher(), sender, TimeProvider.System);
    private sealed class FakeSender : IVerificationEmailSender { public string? Code { get; private set; } public Task<string?> SendAsync(string email, string code, CancellationToken cancellationToken = default) { Code = code; return Task.FromResult<string?>(code); } }
    private sealed class FakeExternalVerifier(ExternalIdentity? identity) : IExternalIdentityVerifier { public Task<ExternalIdentity?> VerifyGoogleAsync(string credential, CancellationToken cancellationToken = default) => Task.FromResult(identity); }
}
