using Inventory.Application.Reporting;
using Inventory.Domain.Identity;
using Inventory.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.API.Controllers.v1;

[ApiController]
[Route("api/v1/reports")]
[Authorize]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportingService _reportingService;
    private readonly IConfiguration _configuration;

    public ReportsController(IReportingService reportingService, IConfiguration configuration)
    {
        _reportingService = reportingService;
        _configuration = configuration;
    }

    [HttpGet("dashboard-summary")]
    [HasPermission(Permissions.Reports.Read)]
    [ProducesResponseType(typeof(DashboardSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardSummaryDto>> GetDashboardSummary(CancellationToken cancellationToken)
    {
        var isDemo = _configuration.GetValue<bool>("Seed:EnableDemoData");
        var result = await _reportingService.GetDashboardSummaryAsync(isDemo, cancellationToken);
        return Ok(result);
    }

    [HttpGet("inventory-valuation")]
    [HasPermission(Permissions.Reports.Read)]
    [ProducesResponseType(typeof(InventoryValuationReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<InventoryValuationReportDto>> GetInventoryValuation(CancellationToken cancellationToken)
    {
        var result = await _reportingService.GetInventoryValuationReportAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("low-stock")]
    [HasPermission(Permissions.Reports.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<LowStockAlertItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LowStockAlertItemDto>>> GetLowStock(
        [FromQuery] Guid? warehouseId,
        CancellationToken cancellationToken)
    {
        var result = await _reportingService.GetLowStockReportAsync(warehouseId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("order-statistics")]
    [HasPermission(Permissions.Reports.Read)]
    [ProducesResponseType(typeof(OrderStatisticsReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderStatisticsReportDto>> GetOrderStatistics(CancellationToken cancellationToken)
    {
        var result = await _reportingService.GetOrderStatisticsReportAsync(cancellationToken);
        return Ok(result);
    }
}
