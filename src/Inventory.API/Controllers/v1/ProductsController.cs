using Inventory.Application.Catalog;
using Inventory.Application.Common;
using Inventory.Domain.Identity;
using Inventory.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.API.Controllers.v1;

[ApiController]
[Route("api/v1/products")]
[Authorize]
public sealed class ProductsController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public ProductsController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet]
    [HasPermission(Permissions.Catalog.Read)]
    [ProducesResponseType(typeof(PagedResponse<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ProductDto>>> GetProducts(
        [FromQuery] ProductFilterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _catalogService.GetProductsAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{productId:guid}")]
    [HasPermission(Permissions.Catalog.Read)]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductDto>> GetProductById(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var result = await _catalogService.GetProductByIdAsync(productId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.Catalog.Write)]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ProductDto>> CreateProduct(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _catalogService.CreateProductAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetProductById), new { productId = result.Id }, result);
    }

    [HttpPut("{productId:guid}")]
    [HasPermission(Permissions.Catalog.Write)]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductDto>> UpdateProduct(
        Guid productId,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _catalogService.UpdateProductAsync(productId, request, cancellationToken);
        return Ok(result);
    }
}
