namespace GymShop.Application.Abstractions;

public interface IArcaElectronicInvoiceGateway
{
    Task<ArcaConnectionStatus> CheckConnectionAsync(CancellationToken cancellationToken = default);
    Task<ArcaInvoiceSequence> GetNextHomologationInvoiceSequenceAsync(CancellationToken cancellationToken = default);
    Task<ArcaInvoiceAuthorization> AuthorizeHomologationInvoiceAsync(
        ArcaInvoiceAuthorizationRequest request,
        CancellationToken cancellationToken = default);
    Task<ArcaInvoiceSequence> GetNextHomologationCreditNoteSequenceAsync(CancellationToken cancellationToken = default);
    Task<ArcaInvoiceAuthorization> AuthorizeHomologationCreditNoteAsync(
        ArcaCreditNoteAuthorizationRequest request,
        CancellationToken cancellationToken = default);
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

public sealed record ArcaInvoiceSequence(int PointOfSale, int InvoiceType, long DocumentNumber);

public sealed record ArcaInvoiceAuthorizationRequest(
    int PointOfSale,
    int InvoiceType,
    long DocumentNumber,
    DateOnly IssuedOn,
    decimal Total);

public sealed record ArcaCreditNoteAuthorizationRequest(
    int PointOfSale,
    int CreditNoteType,
    long DocumentNumber,
    DateOnly IssuedOn,
    decimal Total,
    int AssociatedInvoiceType,
    int AssociatedPointOfSale,
    long AssociatedDocumentNumber,
    DateOnly AssociatedIssuedOn);

public sealed record ArcaInvoiceAuthorization(
    bool Authorized,
    int PointOfSale,
    int InvoiceType,
    long DocumentNumber,
    string? Cae,
    DateOnly? CaeExpiresOn,
    string? RejectionCode,
    string? RejectionReason);
