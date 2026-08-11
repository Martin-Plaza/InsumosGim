namespace GymShop.Application.Abstractions;

public interface IVerificationEmailSender
{
    Task<string?> SendAsync(string email, string code, CancellationToken cancellationToken = default);
}

public sealed record ExternalIdentity(string Provider, string Subject, string Email, bool EmailVerified, string FirstName, string? LastName);

public interface IExternalIdentityVerifier
{
    Task<ExternalIdentity?> VerifyGoogleAsync(string credential, CancellationToken cancellationToken = default);
}
