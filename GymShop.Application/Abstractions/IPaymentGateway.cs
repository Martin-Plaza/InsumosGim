using GymShop.Domain.Entities;

namespace GymShop.Application.Abstractions;

public record PaymentPreferenceResult(
    string Provider,
    string ProviderPreferenceId,
    string CheckoutUrl
);

public record ProviderPaymentResult(
    string ProviderPaymentId,
    string ExternalReference,
    string Status,
    decimal Amount,
    string Currency,
    string? FailureReason
);

public interface IPaymentGateway
{
    bool CanHandle(string provider);
    Task<PaymentPreferenceResult> CreatePreferenceAsync(Order order, string? idempotencyKey, CancellationToken cancellationToken = default);
    Task<ProviderPaymentResult> GetPaymentAsync(string providerPaymentId, CancellationToken cancellationToken = default);
}

public class PaymentGatewayException : Exception
{
    public PaymentGatewayException(string message) : base(message)
    {
    }
}
