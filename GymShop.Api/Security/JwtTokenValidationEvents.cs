using System.Globalization;
using System.Security.Claims;
using GymShop.Application.Abstractions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace GymShop.Api.Security;

public sealed class JwtTokenValidationEvents : JwtBearerEvents
{
    private readonly IApplicationDbContext _db;

    public JwtTokenValidationEvents(IApplicationDbContext db)
    {
        _db = db;
    }

    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var tokenVersionValue = context.Principal?.FindFirstValue(JwtClaimNames.TokenVersion);
        var tokenRole = context.Principal?.FindFirstValue(ClaimTypes.Role);

        if (!int.TryParse(userIdValue, NumberStyles.None, CultureInfo.InvariantCulture, out var userId) ||
            !int.TryParse(tokenVersionValue, NumberStyles.None, CultureInfo.InvariantCulture, out var tokenVersion) ||
            string.IsNullOrWhiteSpace(tokenRole))
        {
            context.Fail("The token is no longer valid.");
            return;
        }

        var currentUser = await _db.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new
            {
                user.IsActive,
                user.TokenVersion,
                Role = user.Role.Name
            })
            .SingleOrDefaultAsync(context.HttpContext.RequestAborted);

        if (currentUser is null ||
            !currentUser.IsActive ||
            currentUser.TokenVersion != tokenVersion ||
            !string.Equals(currentUser.Role, tokenRole, StringComparison.Ordinal))
        {
            context.Fail("The token is no longer valid.");
        }
    }
}
