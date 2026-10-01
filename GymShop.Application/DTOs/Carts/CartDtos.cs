using System.ComponentModel.DataAnnotations;
using GymShop.Application.Common;

namespace GymShop.Application.DTOs.Carts;

public record AddCartItemRequest(int ProductId, int Quantity, int? ProductVariantId = null);
public record UpdateCartItemRequest(int Quantity);
public record CheckoutCartRequest(
    [Required, StringLength(30)] string DeliveryMethod,
    [StringLength(ValidationLimits.ShippingAddress)] string? ShippingAddress,
    [NonNegativeSqlDecimal] decimal ExpectedShippingCost,
    [SqlDecimal] decimal? ExpectedSubtotal = null,
    [NonNegativeSqlDecimal] decimal? ExpectedDiscount = null,
    [StringLength(ValidationLimits.IdempotencyKey)] string? IdempotencyKey = null,
    Guid? ShippingQuoteId = null,
    ShippingAddressRequest? ShippingDestination = null);

public sealed record ShippingAddressRequest(
    [Required, StringLength(ValidationLimits.ShippingPostalCode)] string PostalCode,
    [Required, StringLength(ValidationLimits.ShippingProvince)] string Province,
    [Required, StringLength(ValidationLimits.ShippingCity)] string City,
    [Required, StringLength(ValidationLimits.ShippingStreet)] string Street,
    [Required, StringLength(ValidationLimits.ShippingStreetNumber)] string StreetNumber,
    [StringLength(ValidationLimits.ShippingFloor)] string? Floor = null,
    [StringLength(ValidationLimits.ShippingApartment)] string? Apartment = null,
    [StringLength(ValidationLimits.ShippingNotes)] string? Notes = null);

public sealed record CreateShippingQuoteRequest([Required] ShippingAddressRequest Destination);

public sealed record ShippingQuoteResponse(
    Guid Id,
    string ProviderCode,
    string ServiceCode,
    string ServiceName,
    decimal Price,
    DateTime EstimatedDeliveryFrom,
    DateTime EstimatedDeliveryTo,
    DateTime ExpiresAtUtc);

public record ShippingOptionsResponse(decimal HomeDeliveryCost, string PickupAddress, string PickupInstructions, string PickupHours);

public record CartResponse(int Id, int UserId, decimal Subtotal, decimal Discount, decimal Total, string? CouponCode, List<CartItemResponse> Items);
public record CartItemResponse(int ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal Subtotal, int Stock, string? ImageUrl,
    int? ProductVariantId = null, string? VariantSku = null, Dictionary<string, string>? VariantAttributes = null);
