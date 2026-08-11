using System.Net.Http.Json;
using System.Text.Json.Serialization;
using GymShop.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GymShop.Infrastructure.Services;

public sealed class MockVerificationEmailSender(ILogger<MockVerificationEmailSender> logger) : IVerificationEmailSender
{
    public Task<string?> SendAsync(string email, string code, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Mock verification email generated for {Email}. Code: {VerificationCode}", email, code);
        return Task.FromResult<string?>(code);
    }
}

public sealed class GoogleIdentityVerifier(HttpClient client, IConfiguration configuration) : IExternalIdentityVerifier
{
    public async Task<ExternalIdentity?> VerifyGoogleAsync(string credential, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(credential)) return null;
        using var response = await client.GetAsync($"tokeninfo?id_token={Uri.EscapeDataString(credential)}", cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        var token = await response.Content.ReadFromJsonAsync<GoogleTokenInfo>(cancellationToken: cancellationToken);
        var clientId = configuration["GoogleAuth:ClientId"];
        if (token is null || string.IsNullOrWhiteSpace(clientId) || token.Audience != clientId || token.EmailVerified != "true" || string.IsNullOrWhiteSpace(token.Subject) || string.IsNullOrWhiteSpace(token.Email)) return null;
        return new ExternalIdentity("Google", token.Subject, token.Email, true, token.GivenName ?? token.Email.Split('@')[0], token.FamilyName);
    }

    private sealed record GoogleTokenInfo(
        [property: JsonPropertyName("aud")] string? Audience,
        [property: JsonPropertyName("sub")] string? Subject,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("email_verified")] string? EmailVerified,
        [property: JsonPropertyName("given_name")] string? GivenName,
        [property: JsonPropertyName("family_name")] string? FamilyName);
}
