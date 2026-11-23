using EnterpriseOMS.Api.Contracts.Warehouses;
using EnterpriseOMS.Application.Common.Models;
using EnterpriseOMS.Application.Common.Security;
using EnterpriseOMS.Application.Modules.Warehouses.Commands.CreateStorageLocation;
using EnterpriseOMS.Application.Modules.Warehouses.Commands.CreateWarehouse;
using EnterpriseOMS.Application.Modules.Warehouses.Commands.SetStorageLocationActive;
using EnterpriseOMS.Application.Modules.Warehouses.Commands.SetWarehouseActive;
using EnterpriseOMS.Application.Modules.Warehouses.Commands.UpdateStorageLocation;
using EnterpriseOMS.Application.Modules.Warehouses.Commands.UpdateWarehouse;
using EnterpriseOMS.Application.Modules.Warehouses.DTOs;
using EnterpriseOMS.Application.Modules.Warehouses.Queries.GetWarehouseById;
using EnterpriseOMS.Application.Modules.Warehouses.Queries.GetWarehouseLocations;
using EnterpriseOMS.Application.Modules.Warehouses.Queries.GetWarehousesList;
using EnterpriseOMS.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseOMS.Api.Controllers;

/// <summary>
/// Warehouses of the caller's organization, and the storage locations inside them. As with products,
/// there is no organization id anywhere: it always comes from the token and the database filters by it.
/// </summary>
[ApiController]
[Route("api/warehouses")]
[Produces("application/json")]
public class WarehousesController : ControllerBase
{
    private readonly ISender _sender;

    public WarehousesController(ISender sender)
    {
        _sender = sender;
    }

    // ---------------- warehouses ----------------

    /// <summary>One page of warehouses, with optional search and status filter, and sorting.</summary>
    [HttpGet]
    [HasPermission(Permissions.Warehouses.View)]
    [ProducesResponseType(typeof(PaginatedList<WarehouseListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginatedList<WarehouseListItemDto>>> GetList(
        [FromQuery] GetWarehousesListQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(query, cancellationToken));
    }

    /// <summary>One warehouse, including its Version for later updates.</summary>
    [HttpGet("{id:int}", Name = "GetWarehouseById")]
    [HasPermission(Permissions.Warehouses.View)]
    [ProducesResponseType(typeof(WarehouseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarehouseDto>> GetById(int id, CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(new GetWarehouseByIdQuery { Id = id }, cancellationToken));
    }

    /// <summary>Creates a warehouse in the caller's organization.</summary>
    [HttpPost]
    [HasPermission(Permissions.Warehouses.Create)]
    [ProducesResponseType(typeof(WarehouseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WarehouseDto>> Create(
        [FromBody] CreateWarehouseCommand command,
        CancellationToken cancellationToken)
    {
        var warehouse = await _sender.Send(command, cancellationToken);

        return CreatedAtRoute("GetWarehouseById", new { id = warehouse.Id }, warehouse);
    }

    /// <summary>Updates a warehouse's name and address. Fails with 409 if its Version is out of date.</summary>
    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Warehouses.Update)]
    [ProducesResponseType(typeof(WarehouseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WarehouseDto>> Update(
        int id,
        [FromBody] UpdateWarehouseRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateWarehouseCommand
        {
            Id = id,
            Version = request.Version,
            Name = request.Name,
            Address = request.Address
        };

        return Ok(await _sender.Send(command, cancellationToken));
    }

    /// <summary>Deactivates a warehouse (it stays in the system but is no longer active).</summary>
    [HttpPost("{id:int}/deactivate")]
    [HasPermission(Permissions.Warehouses.Update)]
    [ProducesResponseType(typeof(WarehouseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WarehouseDto>> Deactivate(
        int id,
        [FromBody] WarehouseVersionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SetWarehouseActiveCommand { Id = id, Version = request.Version, IsActive = false };

        return Ok(await _sender.Send(command, cancellationToken));
    }

    /// <summary>Reactivates a previously deactivated warehouse.</summary>
    [HttpPost("{id:int}/activate")]
    [HasPermission(Permissions.Warehouses.Update)]
    [ProducesResponseType(typeof(WarehouseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WarehouseDto>> Activate(
        int id,
        [FromBody] WarehouseVersionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SetWarehouseActiveCommand { Id = id, Version = request.Version, IsActive = true };

        return Ok(await _sender.Send(command, cancellationToken));
    }

    // ---------------- storage locations ----------------

    /// <summary>All locations of a warehouse, in tree order (each parent followed by its descendants).</summary>
    [HttpGet("{id:int}/locations")]
    [HasPermission(Permissions.Warehouses.View)]
    [ProducesResponseType(typeof(List<StorageLocationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<StorageLocationDto>>> GetLocations(
        int id,
        [FromQuery] bool activeOnly,
        CancellationToken cancellationToken)
    {
        var query = new GetWarehouseLocationsQuery { WarehouseId = id, ActiveOnly = activeOnly };

        return Ok(await _sender.Send(query, cancellationToken));
    }

    /// <summary>Adds a location to a warehouse, optionally inside an existing location.</summary>
    [HttpPost("{id:int}/locations")]
    [HasPermission(Permissions.Warehouses.Update)]
    [ProducesResponseType(typeof(StorageLocationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<StorageLocationDto>> CreateLocation(
        int id,
        [FromBody] CreateStorageLocationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateStorageLocationCommand
        {
            WarehouseId = id,
            ParentId = request.ParentId,
            Code = request.Code,
            Name = request.Name,
            Type = request.Type
        };

        var location = await _sender.Send(command, cancellationToken);

        // 201 Created; the Location header points at the warehouse's location list.
        return CreatedAtAction(nameof(GetLocations), new { id }, location);
    }

    /// <summary>Renames a location (its code, type and parent never change).</summary>
    [HttpPut("{warehouseId:int}/locations/{id:int}")]
    [HasPermission(Permissions.Warehouses.Update)]
    [ProducesResponseType(typeof(StorageLocationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StorageLocationDto>> UpdateLocation(
        int warehouseId,
        int id,
        [FromBody] UpdateStorageLocationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateStorageLocationCommand
        {
            WarehouseId = warehouseId,
            Id = id,
            Version = request.Version,
            Name = request.Name
        };

        return Ok(await _sender.Send(command, cancellationToken));
    }

    /// <summary>Deactivates a location. Refused (422) while it still has active sub-locations.</summary>
    [HttpPost("{warehouseId:int}/locations/{id:int}/deactivate")]
    [HasPermission(Permissions.Warehouses.Update)]
    [ProducesResponseType(typeof(StorageLocationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<StorageLocationDto>> DeactivateLocation(
        int warehouseId,
        int id,
        [FromBody] WarehouseVersionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SetStorageLocationActiveCommand
        {
            WarehouseId = warehouseId,
            Id = id,
            Version = request.Version,
            IsActive = false
        };

        return Ok(await _sender.Send(command, cancellationToken));
    }

    /// <summary>Reactivates a location. Refused (422) while its parent is inactive.</summary>
    [HttpPost("{warehouseId:int}/locations/{id:int}/activate")]
    [HasPermission(Permissions.Warehouses.Update)]
    [ProducesResponseType(typeof(StorageLocationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<StorageLocationDto>> ActivateLocation(
        int warehouseId,
        int id,
        [FromBody] WarehouseVersionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SetStorageLocationActiveCommand
        {
            WarehouseId = warehouseId,
            Id = id,
            Version = request.Version,
            IsActive = true
        };

        return Ok(await _sender.Send(command, cancellationToken));
    }
}