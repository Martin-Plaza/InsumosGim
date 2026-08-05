using System.ComponentModel.DataAnnotations;
using GymShop.Application.Common;

namespace GymShop.Application.DTOs.Payments;

public record CreatePaymentRequest(
    [StringLength(ValidationLimits.PaymentProvider)] string? Provider,
    [StringLength(ValidationLimits.IdempotencyKey)] string? IdempotencyKey);

public record UpdatePaymentStatusRequest(
    [Required, StringLength(30)] string Status,
    [StringLength(ValidationLimits.PaymentProviderId)] string? ProviderPaymentId,
    [StringLength(ValidationLimits.PaymentFailureReason)] string? FailureReason);

public record PaymentResponse(int Id, int OrderId, string Provider, string ExternalReference,
    string? ProviderPreferenceId, string? ProviderPaymentId, string? IdempotencyKey,
    decimal Amount, string Currency, string Status, string? CheckoutUrl, string? FailureReason,
    DateTime CreatedAt, DateTime? UpdatedAt, DateTime? PaidAt);
