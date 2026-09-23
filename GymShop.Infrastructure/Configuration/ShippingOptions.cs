using GymShop.Application.Abstractions;

namespace GymShop.Infrastructure.Configuration;

public sealed class ShippingOptions : IShippingSettings
{
    public const string SectionName = "Shipping";
    public decimal HomeDeliveryCost { get; set; }
    public string PickupAddress { get; set; } = string.Empty;
    public string PickupInstructions { get; set; } = string.Empty;
    public string PickupHours { get; set; } = string.Empty;
}
