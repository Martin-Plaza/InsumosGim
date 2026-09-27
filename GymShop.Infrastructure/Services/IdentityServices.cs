using System.Net.Http.Json;
using Google.Apis.Auth;
using GymShop.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using GymShop.Infrastructure.Configuration;

namespace GymShop.Infrastructure.Services;

public sealed class MockVerificationEmailSender(ILogger<MockVerificationEmailSender> logger) : IVerificationEmailSender
{
    public Task<EmailSendResult> SendAsync(string email, string code, bool deliver = true, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Mock verification email generated for {Email}. Code: {VerificationCode}", email, code);
        return Task.FromResult(EmailSendResult.Accepted(code));
    }
}

public sealed class MockPasswordResetEmailSender(ILogger<MockPasswordResetEmailSender> logger) : IPasswordResetEmailSender
{
    public Task<EmailSendResult> SendAsync(string email, string code, bool deliver = true, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Mock password-reset email generated for {Email}. Code: {PasswordResetCode}", email, code);
        return Task.FromResult(EmailSendResult.Accepted(code));
    }
}

public sealed class ResendEmailSender(
    HttpClient client,
    IOptions<EmailOptions> options,
    ILogger<ResendEmailSender> logger) : IVerificationEmailSender, IPasswordResetEmailSender
{
    private readonly EmailOptions _options = options.Value;

    Task<EmailSendResult> IVerificationEmailSender.SendAsync(string email, string code, bool deliver, CancellationToken cancellationToken) =>
        deliver ? SendAsync(email, "verification", "Verificá tu email en GymShop", "Código de verificación", code, cancellationToken) : Task.FromResult(EmailSendResult.NotAttempted());

    Task<EmailSendResult> IPasswordResetEmailSender.SendAsync(string email, string code, bool deliver, CancellationToken cancellationToken) =>
        deliver ? SendAsync(email, "password-reset", "Recuperá tu contraseña de GymShop", "Código de recuperación", code, cancellationToken) : Task.FromResult(EmailSendResult.NotAttempted());

    private async Task<EmailSendResult> SendAsync(string email, string purpose, string subject, string heading, string code, CancellationToken cancellationToken)
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
                logger.LogError("Transactional email request failed. Provider {Provider}; Purpose {Purpose}; FailureType {FailureType}; StatusCode {StatusCode}.",
                    "Resend", purpose, EmailSendFailureType.HttpRejected, (int)response.StatusCode);
                return EmailSendResult.Failed(EmailSendFailureType.HttpRejected, (int)response.StatusCode);
            }

            logger.LogInformation("Transactional email request accepted by provider. Provider {Provider}; Purpose {Purpose}; StatusCode {StatusCode}.",
                "Resend", purpose, (int)response.StatusCode);
            return EmailSendResult.Accepted();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError("Transactional email request failed. Provider {Provider}; Purpose {Purpose}; FailureType {FailureType}.",
                "Resend", purpose, EmailSendFailureType.Timeout);
            return EmailSendResult.Failed(EmailSendFailureType.Timeout);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError("Transactional email request failed. Provider {Provider}; Purpose {Purpose}; FailureType {FailureType}; ExceptionType {ExceptionType}.",
                "Resend", purpose, EmailSendFailureType.Network, exception.GetType().Name);
            return EmailSendResult.Failed(EmailSendFailureType.Network);
        }
    }
}

public sealed class GoogleIdentityVerifier(IConfiguration configuration) : IExternalIdentityVerifier
{
    public async Task<ExternalIdentity?> VerifyGoogleAsync(string credential, CancellationToken cancellationToken = default)
    {
        var clientId = configuration["GoogleAuth:ClientId"];
        if (string.IsNullOrWhiteSpace(credential) || string.IsNullOrWhiteSpace(clientId)) return null;

        try
        {
            var token = await GoogleJsonWebSignature.ValidateAsync(credential, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [clientId]
            });
            cancellationToken.ThrowIfCancellationRequested();
            if (!token.EmailVerified || string.IsNullOrWhiteSpace(token.Subject) || string.IsNullOrWhiteSpace(token.Email)) return null;
            return new ExternalIdentity("Google", token.Subject, token.Email, true, token.GivenName ?? token.Email.Split('@')[0], token.FamilyName);
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}
