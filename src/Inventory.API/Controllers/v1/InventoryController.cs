using Inventory.API.Filters;
using Inventory.Application.Common;
using Inventory.Application.Inventory;
using Inventory.Domain.Identity;
using Inventory.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.API.Controllers.v1;

[ApiController]
[Route("api/v1/inventory")]
[Authorize]
public sealed class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet]
    [HasPermission(Permissions.Inventory.Read)]
    [ProducesResponseType(typeof(PagedResponse<InventoryBalanceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<InventoryBalanceDto>>> GetInventoryBalances(
        [FromQuery] InventoryFilterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryService.GetInventoryBalancesAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("movements")]
    [HasPermission(Permissions.Inventory.Read)]
    [ProducesResponseType(typeof(PagedResponse<StockMovementDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<StockMovementDto>>> GetStockMovements(
        [FromQuery] StockMovementFilterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryService.GetStockMovementsAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("reservations")]
    [HasPermission(Permissions.Inventory.Read)]
    [ProducesResponseType(typeof(PagedResponse<StockReservationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<StockReservationDto>>> GetStockReservations(
        [FromQuery] StockReservationFilterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryService.GetStockReservationsAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("adjustments")]
    [Idempotent]
    [HasPermission(Permissions.Inventory.Adjust)]
    [ProducesResponseType(typeof(InventoryBalanceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<InventoryBalanceDto>> AdjustInventory(
        [FromBody] AdjustInventoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryService.AdjustInventoryAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("reconcile")]
    [Idempotent]
    [HasPermission(Permissions.Inventory.Reconcile)]
    [ProducesResponseType(typeof(ReconciliationResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReconciliationResultDto>> ReconcileInventory(
        [FromBody] ReconcileInventoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryService.ReconcileInventoryAsync(request, cancellationToken);
        return Ok(result);
    }
}
