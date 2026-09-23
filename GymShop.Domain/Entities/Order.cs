using GymShop.Domain.Enums;

namespace GymShop.Domain.Entities;

public class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public decimal Total { get; set; }
    public decimal Subtotal { get; set; }
    public string? CouponCode { get; set; }
    public decimal DiscountAmount { get; set; }
    public DeliveryMethod DeliveryMethod { get; set; } = DeliveryMethod.HomeDelivery;
    public decimal ShippingCost { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public string ShippingAddress { get; set; } = string.Empty;
    public string PickupAddress { get; set; } = string.Empty;
    public string PickupHours { get; set; } = string.Empty;
    public string PickupInstructions { get; set; } = string.Empty;
    public string? Carrier { get; set; }
    public string? TrackingNumber { get; set; }
    public string? TrackingUrl { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public CouponRedemption? CouponRedemption { get; set; }
}


