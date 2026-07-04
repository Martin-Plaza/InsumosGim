namespace GymShop.Application.DTOs.Orders;

public record CreateOrderRequest(
    string ShippingAddress,
    List<CreateOrderItemRequest> Items
);

public record CreateOrderItemRequest(
    int ProductId,
    int Quantity
);

public record UpdateOrderStatusRequest(string Status);

public record OrderResponse(
    int Id,
    int UserId,
    string? UserEmail,
    DateTime CreatedAt,
    decimal Total,
    string Status,
    string ShippingAddress,
    List<OrderItemResponse> Items
);

public record OrderSummaryResponse(
    int Id,
    int UserId,
    string? UserEmail,
    DateTime CreatedAt,
    decimal Total,
    string Status
);

public record OrderItemResponse(
    int ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal Subtotal
);

