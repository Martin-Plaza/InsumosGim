using System.Net.Http.Json;
using System.Text.Json.Serialization;
using GymShop.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using GymShop.Infrastructure.Configuration;

namespace GymShop.Infrastructure.Services;

public sealed class MockVerificationEmailSender(ILogger<MockVerificationEmailSender> logger) : IVerificationEmailSender
{
    public Task<string?> SendAsync(string email, string code, bool deliver = true, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Mock verification email generated for {Email}. Code: {VerificationCode}", email, code);
        return Task.FromResult<string?>(code);
    }
}

public sealed class MockPasswordResetEmailSender(ILogger<MockPasswordResetEmailSender> logger) : IPasswordResetEmailSender
{
    public Task<string?> SendAsync(string email, string code, bool deliver = true, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Mock password-reset email generated for {Email}. Code: {PasswordResetCode}", email, code);
        return Task.FromResult<string?>(code);
    }
}

public sealed class ResendEmailSender(
    HttpClient client,
    IOptions<EmailOptions> options,
    ILogger<ResendEmailSender> logger) : IVerificationEmailSender, IPasswordResetEmailSender
{
    private readonly EmailOptions _options = options.Value;

    Task<string?> IVerificationEmailSender.SendAsync(string email, string code, bool deliver, CancellationToken cancellationToken) =>
        deliver ? SendAsync(email, "Verificá tu email en GymShop", "Código de verificación", code, cancellationToken) : Task.FromResult<string?>(null);

    Task<string?> IPasswordResetEmailSender.SendAsync(string email, string code, bool deliver, CancellationToken cancellationToken) =>
        deliver ? SendAsync(email, "Recuperá tu contraseña de GymShop", "Código de recuperación", code, cancellationToken) : Task.FromResult<string?>(null);

    private async Task<string?> SendAsync(string email, string subject, string heading, string code, CancellationToken cancellationToken)
    {
        var from = string.IsNullOrWhiteSpace(_options.FromName)
            ? _options.FromAddress
            : $"{_options.FromName} <{_options.FromAddress}>";
        var payload = new
        {
            from,
            to = new[] { email },
            subject,
            html = $"<h1>{heading}</h1><p>Tu código es:</p><p style=\"font-size:28px;font-weight:700;letter-spacing:6px\">{code}</p><p>Si no solicitaste este mensaje, podés ignorarlo.</p>"
        };

        try
        {
            using var response = await client.PostAsJsonAsync("emails", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Transactional email delivery failed with HTTP status {StatusCode}.", (int)response.StatusCode);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError("Transactional email delivery timed out.");
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Transactional email delivery failed.");
        }

        return null;
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
