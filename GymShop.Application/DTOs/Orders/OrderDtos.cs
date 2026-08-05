using System.ComponentModel.DataAnnotations;
using GymShop.Application.Common;

namespace GymShop.Application.DTOs.Orders;

public record UpdateOrderStatusRequest([Required, StringLength(30)] string Status);

public record OrderFilterRequest(string? UserEmail);

public record CancelOrderRequest([StringLength(ValidationLimits.CancellationReason)] string? Reason);

public record ExpirePendingOrdersRequest(int OlderThanMinutes);

public record ExpirePendingOrdersResponse(int CanceledOrders);

public record OrderResponse(
    int Id,
    int UserId,
    string? UserEmail,
    DateTime CreatedAt,
    decimal Total,
    string Status,
    string ShippingAddress,
    List<OrderItemResponse> Items,
    List<OrderPaymentResponse> Payments
);

public record OrderSummaryResponse(
    int Id,
    int UserId,
    string? UserEmail,
    DateTime CreatedAt,
    decimal Total,
    string Status,
    string? LastPaymentStatus,
    int? LastPaymentId
);

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



