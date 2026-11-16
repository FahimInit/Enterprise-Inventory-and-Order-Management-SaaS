using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Inventory.Application.Common;
using Inventory.Domain.Common;
using Inventory.Infrastructure.Identity;
using Inventory.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Inventory.API.Middleware;

public sealed class TenantResolutionMiddleware
{
    public const string TenantHeaderName = "X-Tenant-Id";
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        TenantContext tenantContext,
        IApplicationDbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                              ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);

            var emailClaim = context.User.FindFirstValue(JwtRegisteredClaimNames.Email)
                             ?? context.User.FindFirstValue(ClaimTypes.Email);

            var isPlatformAdmin = string.Equals(
                context.User.FindFirstValue(TokenService.PlatformAdminClaimType),
                "true",
                StringComparison.OrdinalIgnoreCase);

            if (Guid.TryParse(userIdClaim, out var userId))
            {
                Guid? targetTenantId = null;

                if (context.Request.Headers.TryGetValue(TenantHeaderName, out var headerValues) &&
                    !string.IsNullOrWhiteSpace(headerValues))
                {
                    if (!Guid.TryParse(headerValues.ToString(), out var parsedHeaderTenantId))
                    {
                        throw new RequestValidationException(TenantHeaderName, "The X-Tenant-Id header must be a valid UUID.");
                    }

                    targetTenantId = parsedHeaderTenantId;
                }
                else
                {
                    var tokenTenantClaim = context.User.FindFirstValue(TokenService.TenantIdClaimType);
                    if (Guid.TryParse(tokenTenantClaim, out var parsedClaimTenantId))
                    {
                        targetTenantId = parsedClaimTenantId;
                    }
                }

                if (targetTenantId.HasValue && targetTenantId.Value != Guid.Empty)
                {
                    var requestedTenantId = targetTenantId.Value;

                    var activeMembership = await (
                        from m in dbContext.OrganizationMembers.IgnoreQueryFilters().AsNoTracking()
                        join o in dbContext.Organizations.AsNoTracking() on m.TenantId equals o.Id
                        where m.UserId == userId &&
                              m.TenantId == requestedTenantId &&
                              m.IsActive &&
                              o.IsActive
                        select new { m.TenantId, m.RoleId })
                        .FirstOrDefaultAsync(context.RequestAborted);

                    if (activeMembership is null)
                    {
                        throw new ForbiddenAccessException(
                            "Tenant.MembershipRequired",
                            "You do not have an active membership in the target organization workspace.");
                    }

                    var permissions = await dbContext.RolePermissions
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .Where(rp => rp.TenantId == activeMembership.TenantId && rp.RoleId == activeMembership.RoleId)
                        .Select(rp => rp.Permission)
                        .ToListAsync(context.RequestAborted);

                    tenantContext.SetTenantContext(
                        activeMembership.TenantId,
                        userId,
                        emailClaim,
                        permissions,
                        isPlatformAdmin);
                }
                else
                {
                    tenantContext.SetUserOnly(userId, emailClaim, isPlatformAdmin);
                }
            }
        }

        await _next(context);
    }
}
