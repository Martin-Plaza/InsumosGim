using GymShop.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymShop.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,SuperAdmin")]
[Route("api/admin/billing")]
public sealed class BillingController(IBillingProfile profile) : ControllerBase
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
