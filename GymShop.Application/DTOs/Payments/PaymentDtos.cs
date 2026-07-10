namespace GymShop.Application.DTOs.Payments;

public record CreatePaymentRequest(
    string? Provider,
    string? IdempotencyKey
);

public record UpdatePaymentStatusRequest(
    string Status,
    string? ProviderPaymentId,
    string? FailureReason
);

public record PaymentResponse(
    int Id,
    int OrderId,
    string Provider,
    string ExternalReference,
    string? ProviderPreferenceId,
    string? ProviderPaymentId,
    decimal Amount,
    string Currency,
    string Status,
    string? CheckoutUrl,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? PaidAt
);

