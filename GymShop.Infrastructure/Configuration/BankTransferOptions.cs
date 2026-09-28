namespace GymShop.Infrastructure.Configuration;

public sealed class BankTransferOptions
{
    public const string SectionName = "BankTransfer";
    public string BankName { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public string Cbu { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public string Cuit { get; set; } = string.Empty;
}
