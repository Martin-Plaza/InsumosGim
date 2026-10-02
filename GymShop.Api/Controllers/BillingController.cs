using GymShop.Application.Abstractions;
using GymShop.Application.DTOs.Billing;
using GymShop.Application.UseCases.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymShop.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,SuperAdmin")]
[Route("api/admin/billing")]
public sealed class BillingController(
    IBillingProfile profile,
    ICreateOrderReceiptUseCase createOrderReceipt,
    IGetOrderBillingDocumentsUseCase getOrderBillingDocuments) : ApiControllerBase
{
    [HttpGet("profile")]
    [ProducesResponseType(typeof(BillingProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<BillingProfileResponse> GetProfile() => Ok(new BillingProfileResponse(
        profile.Mode.ToString(),
        profile.TaxCondition.ToString(),
        profile.BusinessName,
        profile.Cuit,
        profile.FiscalAddress,
        profile.GrossIncomeNumber,
        profile.ActivityStartDate,
        profile.PointOfSale,
        profile.ArcaEnabled,
        profile.ElectronicInvoicingReady));

    [HttpGet("orders/{orderId:int}/documents")]
    [ProducesResponseType(typeof(List<BillingDocumentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<BillingDocumentResponse>>> GetOrderDocuments(int orderId, CancellationToken cancellationToken) =>
        FromResult(await getOrderBillingDocuments.ExecuteAsync(orderId, cancellationToken));

    [HttpPost("orders/{orderId:int}/receipts")]
    [ProducesResponseType(typeof(BillingDocumentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BillingDocumentResponse>> CreateReceipt(
        int orderId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken) =>
        FromResult(await createOrderReceipt.ExecuteAsync(orderId, idempotencyKey, cancellationToken));
}

public sealed record BillingProfileResponse(
    string Mode,
    string TaxCondition,
    string BusinessName,
    string Cuit,
    string FiscalAddress,
    string GrossIncomeNumber,
    DateOnly? ActivityStartDate,
    int? PointOfSale,
    bool ArcaEnabled,
    bool ElectronicInvoicingReady);
