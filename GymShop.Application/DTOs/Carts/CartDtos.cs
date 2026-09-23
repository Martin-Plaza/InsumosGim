using System.ComponentModel.DataAnnotations;
using GymShop.Application.Common;

namespace GymShop.Application.DTOs.Carts;

public record AddCartItemRequest(int ProductId, int Quantity);
public record UpdateCartItemRequest(int Quantity);
public record CheckoutCartRequest(
    [Required, StringLength(30)] string DeliveryMethod,
    [StringLength(ValidationLimits.ShippingAddress)] string? ShippingAddress,
    [SqlDecimal] decimal ExpectedShippingCost,
    [SqlDecimal] decimal? ExpectedSubtotal = null,
    [SqlDecimal] decimal? ExpectedDiscount = null);

public record ShippingOptionsResponse(decimal HomeDeliveryCost, string PickupAddress, string PickupInstructions, string PickupHours);

public record CartResponse(int Id, int UserId, decimal Subtotal, decimal Discount, decimal Total, string? CouponCode, List<CartItemResponse> Items);
public record CartItemResponse(int ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal Subtotal, int Stock, string? ImageUrl);
