namespace GymShop.Application.Abstractions;

public interface IShippingSettings
{
    decimal HomeDeliveryCost { get; }
    string PickupAddress { get; }
    string PickupInstructions { get; }
    string PickupHours { get; }
}

public sealed class FreeShippingSettings : IShippingSettings
{
    public decimal HomeDeliveryCost => 0;
    public string PickupAddress => string.Empty;
    public string PickupInstructions => string.Empty;
    public string PickupHours => string.Empty;
}
