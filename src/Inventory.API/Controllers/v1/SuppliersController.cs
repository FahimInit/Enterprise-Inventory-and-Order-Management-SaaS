using Inventory.Application.Common;
using Inventory.Application.Suppliers;
using Inventory.Domain.Identity;
using Inventory.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.API.Controllers.v1;

[ApiController]
[Route("api/v1/suppliers")]
[Authorize]
public sealed class SuppliersController : ControllerBase
{
    private readonly ISupplierService _supplierService;

    public SuppliersController(ISupplierService supplierService)
    {
        _supplierService = supplierService;
    }

    [HttpGet]
    [HasPermission(Permissions.Suppliers.Read)]
    [ProducesResponseType(typeof(PagedResponse<SupplierDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<SupplierDto>>> GetSuppliers(
        [FromQuery] SupplierFilterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _supplierService.GetSuppliersAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{supplierId:guid}")]
    [HasPermission(Permissions.Suppliers.Read)]
    [ProducesResponseType(typeof(SupplierDetailsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SupplierDetailsDto>> GetSupplierById(
        Guid supplierId,
        CancellationToken cancellationToken)
    {
        var result = await _supplierService.GetSupplierByIdAsync(supplierId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.Suppliers.Write)]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<SupplierDto>> CreateSupplier(
        [FromBody] CreateSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _supplierService.CreateSupplierAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetSupplierById), new { supplierId = result.Id }, result);
    }

    [HttpPut("{supplierId:guid}")]
    [HasPermission(Permissions.Suppliers.Write)]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SupplierDto>> UpdateSupplier(
        Guid supplierId,
        [FromBody] UpdateSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _supplierService.UpdateSupplierAsync(supplierId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{supplierId:guid}/products")]
    [HasPermission(Permissions.Suppliers.Write)]
    [ProducesResponseType(typeof(SupplierProductDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SupplierProductDto>> LinkSupplierProduct(
        Guid supplierId,
        [FromBody] LinkSupplierProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _supplierService.LinkSupplierProductAsync(supplierId, request, cancellationToken);
        return Ok(result);
    }
}
