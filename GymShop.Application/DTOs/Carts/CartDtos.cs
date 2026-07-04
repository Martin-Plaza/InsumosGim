namespace GymShop.Application.DTOs.Carts;

public record AddCartItemRequest(int ProductId, int Quantity);

public record UpdateCartItemRequest(int Quantity);

public record CheckoutCartRequest(string ShippingAddress);

public record CartResponse(
    int Id,
    int UserId,
    decimal Total,
    List<CartItemResponse> Items
);

public record CartItemResponse(
    int ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal Subtotal,
    int Stock,
    string? ImageUrl
);
