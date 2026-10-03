namespace GymShop.Application.Abstractions;

public interface IArcaElectronicInvoiceGateway
{
    Task<ArcaConnectionStatus> CheckConnectionAsync(CancellationToken cancellationToken = default);
}

public sealed record ArcaConnectionStatus(
    string Environment,
    bool ConfigurationReady,
    bool WsaaAuthenticated,
    bool WsfeReachable,
    IReadOnlyList<int> PointsOfSale,
    string? ErrorCode,
    string? Message,
    DateTime CheckedAtUtc);
