using Inventory.Application.Common;
using Inventory.Application.StockTransfers;
using Inventory.Domain.Identity;
using Inventory.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.API.Controllers.v1;

[ApiController]
[Route("api/v1/stock-transfers")]
[Authorize]
public sealed class StockTransfersController : ControllerBase
{
    private readonly IStockTransferService _transferService;

    public StockTransfersController(IStockTransferService transferService)
    {
        _transferService = transferService;
    }

    [HttpGet]
    [HasPermission(Permissions.Inventory.Read)]
    [ProducesResponseType(typeof(PagedResponse<StockTransferDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<StockTransferDto>>> GetTransfers(
        [FromQuery] StockTransferFilterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _transferService.GetTransfersAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{transferId:guid}")]
    [HasPermission(Permissions.Inventory.Read)]
    [ProducesResponseType(typeof(StockTransferDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StockTransferDto>> GetTransferById(
        Guid transferId,
        CancellationToken cancellationToken)
    {
        var result = await _transferService.GetTransferByIdAsync(transferId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.Transfers.Create)]
    [ProducesResponseType(typeof(StockTransferDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<StockTransferDto>> CreateTransfer(
        [FromBody] CreateStockTransferRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _transferService.CreateTransferAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetTransferById), new { transferId = result.Id }, result);
    }

    [HttpPost("{transferId:guid}/dispatch")]
    [HasPermission(Permissions.Transfers.Dispatch)]
    [ProducesResponseType(typeof(StockTransferDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StockTransferDto>> DispatchTransfer(
        Guid transferId,
        CancellationToken cancellationToken)
    {
        var result = await _transferService.DispatchTransferAsync(transferId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{transferId:guid}/receive")]
    [HasPermission(Permissions.Transfers.Receive)]
    [ProducesResponseType(typeof(StockTransferDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StockTransferDto>> ReceiveTransfer(
        Guid transferId,
        CancellationToken cancellationToken)
    {
        var result = await _transferService.ReceiveTransferAsync(transferId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{transferId:guid}/cancel")]
    [HasPermission(Permissions.Transfers.Dispatch)]
    [ProducesResponseType(typeof(StockTransferDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StockTransferDto>> CancelTransfer(
        Guid transferId,
        [FromBody] CancelStockTransferRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _transferService.CancelTransferAsync(transferId, request, cancellationToken);
        return Ok(result);
    }
}
