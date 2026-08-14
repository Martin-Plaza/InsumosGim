using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using GymShop.Api.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;

namespace GymShop.Api.RateLimiting;

public readonly record struct RateLimitDecision(bool IsAllowed, TimeSpan RetryAfter);

public interface IGymShopRequestLimiter
{
    RateLimitDecision Acquire(string policy, string partitionKey);
}

public sealed class GymShopRequestLimiter : IGymShopRequestLimiter
{
    private sealed record Counter(long Window, int Count);
    private readonly ConcurrentDictionary<string, Counter> _counters = new();
    private readonly GymShopRateLimitingOptions _options;
    private readonly TimeProvider _timeProvider;
    private long _operations;

    public GymShopRequestLimiter(IOptions<GymShopRateLimitingOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public RateLimitDecision Acquire(string policy, string partitionKey)
    {
        if (!_options.Enabled) return new RateLimitDecision(true, TimeSpan.Zero);
        var rule = GetRule(policy);
        var now = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var window = now / rule.WindowSeconds;
        var key = $"{policy}:{partitionKey}";
        if (Interlocked.Increment(ref _operations) % 1024 == 0)
        {
            foreach (var stale in _counters.Where(x => x.Key.StartsWith(policy + ":", StringComparison.Ordinal) && x.Value.Window < window - 1))
            {
                _counters.TryRemove(stale.Key, out _);
            }
        }

        var counter = _counters.AddOrUpdate(key, _ => new Counter(window, 1), (_, current) =>
            current.Window == window ? current with { Count = current.Count + 1 } : new Counter(window, 1));

        if (counter.Count <= rule.PermitLimit) return new RateLimitDecision(true, TimeSpan.Zero);
        var retrySeconds = ((window + 1) * rule.WindowSeconds) - now;
        return new RateLimitDecision(false, TimeSpan.FromSeconds(Math.Max(1, retrySeconds)));
    }

    private RateLimitRule GetRule(string policy) => policy switch
    {
        RateLimitPolicies.LoginAccount => _options.LoginAccount,
        RateLimitPolicies.RegistrationGlobal => _options.RegistrationGlobal,
        RateLimitPolicies.PasswordResetAccount => _options.PasswordResetAccount,
        RateLimitPolicies.PaymentUser => _options.PaymentUser,
        RateLimitPolicies.PaymentOrder => _options.PaymentOrder,
        RateLimitPolicies.WebhookGlobal => _options.WebhookGlobal,
        _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unknown rate-limit policy.")
    };

    public static string HashAccount(string email) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant())));
}

public static class RateLimitPolicies
{
    public const string LoginIp = "login-ip";
    public const string LoginAccount = "login-account";
    public const string RegistrationIp = "registration-ip";
    public const string RegistrationGlobal = "registration-global";
    public const string PasswordResetAccount = "password-reset-account";
    public const string PasswordResetIp = "password-reset-ip";
    public const string PaymentUser = "payment-user";
    public const string PaymentOrder = "payment-order";
    public const string WebhookIp = "webhook-ip";
    public const string WebhookGlobal = "webhook-global";
}

public static class RateLimitResponse
{
    public static ObjectResult Create(HttpContext context, RateLimitDecision decision)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(decision.RetryAfter.TotalSeconds));
        context.Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Demasiadas solicitudes.",
            Detail = "Se alcanzo el limite temporal de solicitudes. Intenta nuevamente mas tarde."
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;
        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status429TooManyRequests,
            ContentTypes = { "application/problem+json" }
        };
    }
}
