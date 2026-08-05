using System.IdentityModel.Tokens.Jwt;
using GymShop.Application.Common;
using GymShop.Application.DTOs.Auth;
using GymShop.Application.UseCases.Auth;
using GymShop.Infrastructure.Services;
using GymShop.Infrastructure.Configuration;
using GymShop.Tests.TestSupport;
using Microsoft.Extensions.Options;

namespace GymShop.Tests.UseCases;

public class AuthUseCaseTests
{
    [Fact]
    public async Task Register_creates_user_and_returns_token()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var useCase = new RegisterUserUseCase(db, new PasswordHasher(), new FakeJwtTokenService());

        var result = await useCase.ExecuteAsync(new RegisterRequest("Cliente Test", "CLIENTE@TEST.COM", "clave123"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("cliente@test.com", result.Value.User.Email);
        Assert.Equal("User", result.Value.User.Role);
        Assert.StartsWith("test-token-for-", result.Value.Token);
        Assert.Single(db.Users);
    }

    [Fact]
    public async Task Register_rejects_duplicate_email()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var useCase = new RegisterUserUseCase(db, new PasswordHasher(), new FakeJwtTokenService());

        await useCase.ExecuteAsync(new RegisterRequest("Cliente Test", "cliente@test.com", "clave123"));
        var duplicate = await useCase.ExecuteAsync(new RegisterRequest("Otro", "cliente@test.com", "clave123"));

        Assert.False(duplicate.IsSuccess);
        Assert.Equal(AppErrorType.Conflict, duplicate.Error?.Type);
    }

    [Fact]
    public async Task Register_rejects_duplicate_email_with_different_casing()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var useCase = new RegisterUserUseCase(db, new PasswordHasher(), new FakeJwtTokenService());

        await useCase.ExecuteAsync(new RegisterRequest("Cliente Test", "Cliente@Test.com", "clave123"));
        var duplicate = await useCase.ExecuteAsync(new RegisterRequest("Otro", "cliente@test.com", "clave123"));

        Assert.False(duplicate.IsSuccess);
        Assert.Equal(AppErrorType.Conflict, duplicate.Error?.Type);
        Assert.Single(db.Users);
    }
    [Fact]
    public async Task Login_returns_token_for_valid_credentials()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var passwordHasher = new PasswordHasher();
        var register = new RegisterUserUseCase(db, passwordHasher, new FakeJwtTokenService());
        var login = new LoginUserUseCase(db, passwordHasher, new FakeJwtTokenService());

        await register.ExecuteAsync(new RegisterRequest("Cliente Test", "cliente@test.com", "clave123"));
        var result = await login.ExecuteAsync(new LoginRequest("cliente@test.com", "clave123"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("cliente@test.com", result.Value.User.Email);
    }

    [Fact]
    public async Task Login_emits_current_token_version()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var passwordHasher = new PasswordHasher();
        var jwt = new JwtTokenService(Options.Create(new JwtOptions
        {
            Issuer = "GymShop.Tests",
            Audience = "GymShop.Tests.Client",
            Secret = "test-signing-secret-with-at-least-32-characters"
        }));
        var register = new RegisterUserUseCase(db, passwordHasher, new FakeJwtTokenService());
        await register.ExecuteAsync(new RegisterRequest("Cliente Test", "cliente@test.com", "clave123"));
        var user = db.Users.Single();
        user.TokenVersion = 7;
        await db.SaveChangesAsync();
        var login = new LoginUserUseCase(db, passwordHasher, jwt);

        var result = await login.ExecuteAsync(new LoginRequest("cliente@test.com", "clave123"));
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Value!.Token);

        Assert.Equal("7", token.Claims.Single(x => x.Type == GymShop.Application.Abstractions.JwtClaimNames.TokenVersion).Value);
    }

    [Fact]
    public async Task Login_rejects_invalid_password()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var passwordHasher = new PasswordHasher();
        var register = new RegisterUserUseCase(db, passwordHasher, new FakeJwtTokenService());
        var login = new LoginUserUseCase(db, passwordHasher, new FakeJwtTokenService());

        await register.ExecuteAsync(new RegisterRequest("Cliente Test", "cliente@test.com", "clave123"));
        var result = await login.ExecuteAsync(new LoginRequest("cliente@test.com", "wrong-password"));

        Assert.False(result.IsSuccess);
        Assert.Equal(AppErrorType.Unauthorized, result.Error?.Type);
    }
}
