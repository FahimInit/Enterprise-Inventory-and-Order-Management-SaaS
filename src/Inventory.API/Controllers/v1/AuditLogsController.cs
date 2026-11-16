using Inventory.Application.Auditing;
using Inventory.Application.Common;
using Inventory.Domain.Identity;
using Inventory.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.API.Controllers.v1;

[ApiController]
[Route("api/v1/audit-logs")]
[Authorize]
public sealed class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogsController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    [HttpGet]
    [HasPermission(Permissions.AuditLogs.Read)]
    [ProducesResponseType(typeof(PagedResponse<AuditLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AuditLogDto>>> GetAuditLogs(
        [FromQuery] AuditLogFilterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _auditLogService.GetAuditLogsAsync(request, cancellationToken);
        return Ok(result);
    }
}
