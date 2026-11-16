using Inventory.API.Filters;
using Inventory.Application.Common;
using Inventory.Application.PurchaseOrders;
using Inventory.Domain.Identity;
using Inventory.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.API.Controllers.v1;

[ApiController]
[Route("api/v1/purchase-orders")]
[Authorize]
public sealed class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchaseOrderService _purchaseOrderService;

    public PurchaseOrdersController(IPurchaseOrderService purchaseOrderService)
    {
        _purchaseOrderService = purchaseOrderService;
    }

    [HttpGet]
    [HasPermission(Permissions.PurchaseOrders.Read)]
    [ProducesResponseType(typeof(PagedResponse<PurchaseOrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<PurchaseOrderDto>>> GetPurchaseOrders(
        [FromQuery] PurchaseOrderFilterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _purchaseOrderService.GetPurchaseOrdersAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{purchaseOrderId:guid}")]
    [HasPermission(Permissions.PurchaseOrders.Read)]
    [ProducesResponseType(typeof(PurchaseOrderDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PurchaseOrderDto>> GetPurchaseOrderById(
        Guid purchaseOrderId,
        CancellationToken cancellationToken)
    {
        var result = await _purchaseOrderService.GetPurchaseOrderByIdAsync(purchaseOrderId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Idempotent]
    [HasPermission(Permissions.PurchaseOrders.Write)]
    [ProducesResponseType(typeof(PurchaseOrderDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<PurchaseOrderDto>> CreatePurchaseOrder(
        [FromBody] CreatePurchaseOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _purchaseOrderService.CreatePurchaseOrderAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetPurchaseOrderById), new { purchaseOrderId = result.Id }, result);
    }

    [HttpPost("{purchaseOrderId:guid}/submit")]
    [HasPermission(Permissions.PurchaseOrders.Write)]
    [ProducesResponseType(typeof(PurchaseOrderDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PurchaseOrderDto>> SubmitPurchaseOrder(
        Guid purchaseOrderId,
        CancellationToken cancellationToken)
    {
        var result = await _purchaseOrderService.SubmitPurchaseOrderAsync(purchaseOrderId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{purchaseOrderId:guid}/approve")]
    [HasPermission(Permissions.PurchaseOrders.Approve)]
    [ProducesResponseType(typeof(PurchaseOrderDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PurchaseOrderDto>> ApprovePurchaseOrder(
        Guid purchaseOrderId,
        CancellationToken cancellationToken)
    {
        var result = await _purchaseOrderService.ApprovePurchaseOrderAsync(purchaseOrderId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{purchaseOrderId:guid}/receive")]
    [Idempotent]
    [HasPermission(Permissions.PurchaseOrders.Receive)]
    [ProducesResponseType(typeof(PurchaseOrderDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PurchaseOrderDto>> ReceivePurchaseOrder(
        Guid purchaseOrderId,
        [FromBody] ReceivePurchaseOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _purchaseOrderService.ReceivePurchaseOrderAsync(purchaseOrderId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{purchaseOrderId:guid}/cancel")]
    [HasPermission(Permissions.PurchaseOrders.Approve)]
    [ProducesResponseType(typeof(PurchaseOrderDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PurchaseOrderDto>> CancelPurchaseOrder(
        Guid purchaseOrderId,
        [FromBody] CancelPurchaseOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _purchaseOrderService.CancelPurchaseOrderAsync(purchaseOrderId, request, cancellationToken);
        return Ok(result);
    }
}
