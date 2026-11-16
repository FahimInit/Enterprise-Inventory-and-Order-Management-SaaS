using Inventory.Application.Warehouses;
using Inventory.Domain.Identity;
using Inventory.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.API.Controllers.v1;

[ApiController]
[Route("api/v1/warehouses")]
[Authorize]
public sealed class WarehousesController : ControllerBase
{
    private readonly IWarehouseService _warehouseService;

    public WarehousesController(IWarehouseService warehouseService)
    {
        _warehouseService = warehouseService;
    }

    [HttpGet]
    [HasPermission(Permissions.Warehouses.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<WarehouseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WarehouseDto>>> GetWarehouses(CancellationToken cancellationToken)
    {
        var result = await _warehouseService.GetWarehousesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{warehouseId:guid}")]
    [HasPermission(Permissions.Warehouses.Read)]
    [ProducesResponseType(typeof(WarehouseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<WarehouseDto>> GetWarehouseById(
        Guid warehouseId,
        CancellationToken cancellationToken)
    {
        var result = await _warehouseService.GetWarehouseByIdAsync(warehouseId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.Warehouses.Write)]
    [ProducesResponseType(typeof(WarehouseDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<WarehouseDto>> CreateWarehouse(
        [FromBody] CreateWarehouseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _warehouseService.CreateWarehouseAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetWarehouseById), new { warehouseId = result.Id }, result);
    }

    [HttpPut("{warehouseId:guid}")]
    [HasPermission(Permissions.Warehouses.Write)]
    [ProducesResponseType(typeof(WarehouseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<WarehouseDto>> UpdateWarehouse(
        Guid warehouseId,
        [FromBody] UpdateWarehouseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _warehouseService.UpdateWarehouseAsync(warehouseId, request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{warehouseId:guid}/locations")]
    [HasPermission(Permissions.Warehouses.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<WarehouseLocationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WarehouseLocationDto>>> GetLocations(
        Guid warehouseId,
        CancellationToken cancellationToken)
    {
        var result = await _warehouseService.GetLocationsAsync(warehouseId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{warehouseId:guid}/locations")]
    [HasPermission(Permissions.Warehouses.Write)]
    [ProducesResponseType(typeof(WarehouseLocationDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<WarehouseLocationDto>> CreateLocation(
        Guid warehouseId,
        [FromBody] CreateWarehouseLocationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _warehouseService.CreateLocationAsync(warehouseId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
