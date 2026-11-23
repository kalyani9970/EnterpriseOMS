using EnterpriseOMS.Application.Common.Models;
using EnterpriseOMS.Application.Common.Security;
using EnterpriseOMS.Application.Modules.Inventory.Commands.AdjustStock;
using EnterpriseOMS.Application.Modules.Inventory.Commands.IssueStock;
using EnterpriseOMS.Application.Modules.Inventory.Commands.ReceiveStock;
using EnterpriseOMS.Application.Modules.Inventory.DTOs;
using EnterpriseOMS.Application.Modules.Inventory.Queries.GetStockLevels;
using EnterpriseOMS.Application.Modules.Inventory.Queries.GetStockMovements;
using EnterpriseOMS.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseOMS.Api.Controllers;

/// <summary>
/// Stock of the caller's organization: how much of each product is in each warehouse, and the ledger of how
/// it got there. Stock is changed only through receipts, issues and adjustments (never by editing a quantity
/// directly), and each one writes a ledger line. As elsewhere, there is no organization id: it always comes
/// from the token and the database filters by it.
/// </summary>
[ApiController]
[Route("api/inventory")]
[Produces("application/json")]
public class InventoryController : ControllerBase
{
    private readonly ISender _sender;

    public InventoryController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>One page of stock levels, with filters (warehouse, product, search, low stock) and sorting.</summary>
    [HttpGet("stock")]
    [HasPermission(Permissions.Inventory.View)]
    [ProducesResponseType(typeof(PaginatedList<StockLevelListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginatedList<StockLevelListItemDto>>> GetStock(
        [FromQuery] GetStockLevelsQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(query, cancellationToken));
    }

    /// <summary>One page of the stock ledger, newest first, optionally for one product, warehouse or kind of movement.</summary>
    [HttpGet("movements")]
    [HasPermission(Permissions.Inventory.View)]
    [ProducesResponseType(typeof(PaginatedList<StockMovementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginatedList<StockMovementDto>>> GetMovements(
        [FromQuery] GetStockMovementsQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(query, cancellationToken));
    }

    /// <summary>Records stock coming in. Needs an active product and an active warehouse.</summary>
    [HttpPost("receipts")]
    [HasPermission(Permissions.Inventory.Adjust)]
    [ProducesResponseType(typeof(StockMovementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<StockMovementDto>> Receive(
        [FromBody] ReceiveStockCommand command,
        CancellationToken cancellationToken)
    {
        return Created(await _sender.Send(command, cancellationToken));
    }

    /// <summary>Records stock going out. Refused (422) if there is not enough on hand.</summary>
    [HttpPost("issues")]
    [HasPermission(Permissions.Inventory.Adjust)]
    [ProducesResponseType(typeof(StockMovementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<StockMovementDto>> Issue(
        [FromBody] IssueStockCommand command,
        CancellationToken cancellationToken)
    {
        return Created(await _sender.Send(command, cancellationToken));
    }

    /// <summary>Records a stock count: the system's number becomes the counted one, and the difference is logged with the reason.</summary>
    [HttpPost("adjustments")]
    [HasPermission(Permissions.Inventory.Adjust)]
    [ProducesResponseType(typeof(StockMovementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<StockMovementDto>> Adjust(
        [FromBody] AdjustStockCommand command,
        CancellationToken cancellationToken)
    {
        return Created(await _sender.Send(command, cancellationToken));
    }

    /// <summary>201 Created; the Location header points at the ledger, filtered to the product and warehouse that changed.</summary>
    private CreatedAtActionResult Created(StockMovementDto movement) =>
        CreatedAtAction(
            nameof(GetMovements),
            new { productId = movement.ProductId, warehouseId = movement.WarehouseId },
            movement);
}