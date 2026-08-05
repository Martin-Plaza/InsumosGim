using Microsoft.Extensions.Options;

namespace GymShop.Infrastructure.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const string PlaceholderSecret = "SET_WITH_USER_SECRETS_OR_ENVIRONMENT";
    public const int MinimumSecretLength = 32;

    public string? Issuer { get; init; }
    public string? Audience { get; init; }
    public string? Secret { get; init; }
    public int ExpirationMinutes { get; init; } = 10080;
}

public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Secret))
        {
            return ValidateOptionsResult.Fail("Jwt:Secret is required. Configure it with User Secrets or an environment variable.");
        }

        if (string.Equals(options.Secret.Trim(), JwtOptions.PlaceholderSecret, StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail("Jwt:Secret still contains the documented placeholder and must be replaced.");
        }

        if (options.Secret.Length < JwtOptions.MinimumSecretLength)
        {
            return ValidateOptionsResult.Fail($"Jwt:Secret must contain at least {JwtOptions.MinimumSecretLength} characters.");
        }

        return ValidateOptionsResult.Success;
    }
}

public sealed class MercadoPagoOptions
{
    public const string SectionName = "MercadoPago";

    public bool Enabled { get; init; }
    public string? AccessToken { get; init; }
    public string? PublicKey { get; init; }
    public string? WebhookSecret { get; init; }
    public string? NotificationUrl { get; init; }
    public string? SuccessUrl { get; init; }
    public string? FailureUrl { get; init; }
    public string? PendingUrl { get; init; }
    public bool UseSandboxInitPoint { get; init; } = true;
}

public sealed class MercadoPagoOptionsValidator : IValidateOptions<MercadoPagoOptions>
{
    private readonly bool _isProduction;

    public MercadoPagoOptionsValidator(string environmentName)
    {
        _isProduction = string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);
    }

    public ValidateOptionsResult Validate(string? name, MercadoPagoOptions options)
    {
        if (options.Enabled && string.IsNullOrWhiteSpace(options.AccessToken))
        {
            return ValidateOptionsResult.Fail("MercadoPago:AccessToken is required when Mercado Pago is enabled.");
        }

        if (options.Enabled && _isProduction && string.IsNullOrWhiteSpace(options.WebhookSecret))
        {
            return ValidateOptionsResult.Fail("MercadoPago:WebhookSecret is required when Mercado Pago is enabled in Production.");
        }

        return ValidateOptionsResult.Success;
    }
}
