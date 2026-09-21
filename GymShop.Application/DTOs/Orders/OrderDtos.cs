using System.ComponentModel.DataAnnotations;
using GymShop.Application.Common;

namespace GymShop.Application.DTOs.Orders;

public record UpdateOrderStatusRequest([Required, StringLength(30)] string Status, DateTime? ExpectedUpdatedAt = null);

public sealed record OrderFilterRequest(
    [Range(1, int.MaxValue)] int Page = 1,
    [Range(1, 100)] int PageSize = 20,
    [StringLength(150)] string? Search = null,
    [StringLength(30)] string? Status = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null);

public record CancelOrderRequest([StringLength(ValidationLimits.CancellationReason)] string? Reason);

public record ExpirePendingOrdersRequest(int OlderThanMinutes);

public record ExpirePendingOrdersResponse(int CanceledOrders);

public record OrderResponse(
    int Id,
    int UserId,
    string? UserEmail,
    string UserName,
    string? UserPhone,
    DateTime CreatedAt,
    decimal Total,
    string Status,
    string ShippingAddress,
    string? CancellationReason,
    DateTime? UpdatedAt,
    List<OrderItemResponse> Items,
    List<OrderPaymentResponse> Payments
);

public record OrderSummaryResponse(
    int Id,
    int UserId,
    string? UserEmail,
    string UserName,
    DateTime CreatedAt,
    decimal Total,
    string Status,
    DateTime? UpdatedAt,
    string? LastPaymentStatus,
    int? LastPaymentId
);

public sealed record PagedOrdersResponse(
    List<OrderSummaryResponse> Items,
    int Page,
    int PageSize,
    long TotalItems,
    int TotalPages);

public sealed record OrderHistoryEventResponse(
    long Id,
    string Action,
    string? PreviousStatus,
    string? NewStatus,
    string? Reason,
    DateTime CreatedAtUtc,
    int? ActorUserId,
    string? ActorName,
    string? ActorEmail,
    string Source);

public record OrderItemResponse(
    int ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal Subtotal
);

public record OrderPaymentResponse(
    int Id,
    string Provider,
    decimal Amount,
    string Currency,
    string Status,
    DateTime CreatedAt,
    DateTime? PaidAt
);



