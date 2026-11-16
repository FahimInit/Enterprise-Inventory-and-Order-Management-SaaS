using Inventory.API.Filters;
using Inventory.Application.Common;
using Inventory.Application.SalesOrders;
using Inventory.Domain.Identity;
using Inventory.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.API.Controllers.v1;

[ApiController]
[Route("api/v1/sales-orders")]
[Authorize]
public sealed class SalesOrdersController : ControllerBase
{
    private readonly ISalesOrderService _salesOrderService;

    public SalesOrdersController(ISalesOrderService salesOrderService)
    {
        _salesOrderService = salesOrderService;
    }

    [HttpGet]
    [HasPermission(Permissions.SalesOrders.Read)]
    [ProducesResponseType(typeof(PagedResponse<SalesOrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<SalesOrderDto>>> GetSalesOrders(
        [FromQuery] SalesOrderFilterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _salesOrderService.GetSalesOrdersAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{salesOrderId:guid}")]
    [HasPermission(Permissions.SalesOrders.Read)]
    [ProducesResponseType(typeof(SalesOrderDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SalesOrderDto>> GetSalesOrderById(
        Guid salesOrderId,
        CancellationToken cancellationToken)
    {
        var result = await _salesOrderService.GetSalesOrderByIdAsync(salesOrderId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Idempotent]
    [HasPermission(Permissions.SalesOrders.Write)]
    [ProducesResponseType(typeof(SalesOrderDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<SalesOrderDto>> CreateSalesOrder(
        [FromBody] CreateSalesOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _salesOrderService.CreateSalesOrderAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetSalesOrderById), new { salesOrderId = result.Id }, result);
    }

    [HttpPost("{salesOrderId:guid}/confirm")]
    [Idempotent]
    [HasPermission(Permissions.SalesOrders.Confirm)]
    [ProducesResponseType(typeof(SalesOrderDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SalesOrderDto>> ConfirmSalesOrder(
        Guid salesOrderId,
        CancellationToken cancellationToken)
    {
        var result = await _salesOrderService.ConfirmSalesOrderAsync(salesOrderId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{salesOrderId:guid}/process")]
    [HasPermission(Permissions.SalesOrders.Fulfill)]
    [ProducesResponseType(typeof(SalesOrderDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SalesOrderDto>> StartProcessingSalesOrder(
        Guid salesOrderId,
        CancellationToken cancellationToken)
    {
        var result = await _salesOrderService.StartProcessingSalesOrderAsync(salesOrderId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{salesOrderId:guid}/fulfill")]
    [Idempotent]
    [HasPermission(Permissions.SalesOrders.Fulfill)]
    [ProducesResponseType(typeof(SalesOrderDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SalesOrderDto>> FulfillSalesOrder(
        Guid salesOrderId,
        CancellationToken cancellationToken)
    {
        var result = await _salesOrderService.FulfillSalesOrderAsync(salesOrderId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{salesOrderId:guid}/cancel")]
    [Idempotent]
    [HasPermission(Permissions.SalesOrders.Cancel)]
    [ProducesResponseType(typeof(SalesOrderDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SalesOrderDto>> CancelSalesOrder(
        Guid salesOrderId,
        [FromBody] CancelSalesOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _salesOrderService.CancelSalesOrderAsync(salesOrderId, request, cancellationToken);
        return Ok(result);
    }
}
