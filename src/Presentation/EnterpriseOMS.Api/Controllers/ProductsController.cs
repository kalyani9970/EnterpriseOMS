using EnterpriseOMS.Api.Contracts.Products;
using EnterpriseOMS.Application.Common.Models;
using EnterpriseOMS.Application.Common.Security;
using EnterpriseOMS.Application.Modules.Products.Commands.CreateProduct;
using EnterpriseOMS.Application.Modules.Products.Commands.SetProductActive;
using EnterpriseOMS.Application.Modules.Products.Commands.UpdateProduct;
using EnterpriseOMS.Application.Modules.Products.DTOs;
using EnterpriseOMS.Application.Modules.Products.Queries.GetProductById;
using EnterpriseOMS.Application.Modules.Products.Queries.GetProductCategories;
using EnterpriseOMS.Application.Modules.Products.Queries.GetProductsList;
using EnterpriseOMS.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseOMS.Api.Controllers;

/// <summary>
/// Product catalog of the caller's organization. There is no organization id anywhere in the routes or
/// bodies: it always comes from the token, and the database layer filters by it as well.
/// </summary>
[ApiController]
[Route("api/products")]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly ISender _sender;

    public ProductsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>One page of products, with optional search, category and status filters and sorting.</summary>
    [HttpGet]
    [HasPermission(Permissions.Products.View)]
    [ProducesResponseType(typeof(PaginatedList<ProductListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginatedList<ProductListItemDto>>> GetList(
        [FromQuery] GetProductsListQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(query, cancellationToken));
    }

    /// <summary>The distinct categories in use (for the filter drop-down).</summary>
    [HttpGet("categories")]
    [HasPermission(Permissions.Products.View)]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<string>>> GetCategories(CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(new GetProductCategoriesQuery(), cancellationToken));
    }

    /// <summary>One product, including its Version for later updates.</summary>
    [HttpGet("{id:int}", Name = "GetProductById")]
    [HasPermission(Permissions.Products.View)]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetById(int id, CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(new GetProductByIdQuery { Id = id }, cancellationToken));
    }

    /// <summary>Creates a product in the caller's organization.</summary>
    [HttpPost]
    [HasPermission(Permissions.Products.Create)]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductDto>> Create(
        [FromBody] CreateProductCommand command,
        CancellationToken cancellationToken)
    {
        var product = await _sender.Send(command, cancellationToken);

        // 201 Created with a Location header pointing at the new resource: the REST convention for POST.
        return CreatedAtRoute("GetProductById", new { id = product.Id }, product);
    }

    /// <summary>Updates a product. Fails with 409 if its Version is no longer the current one.</summary>
    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Products.Update)]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductDto>> Update(
        int id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateProductCommand
        {
            Id = id,
            Version = request.Version,
            Name = request.Name,
            Description = request.Description,
            Category = request.Category,
            UnitOfMeasure = request.UnitOfMeasure,
            UnitPrice = request.UnitPrice,
            CostPrice = request.CostPrice,
            ReorderLevel = request.ReorderLevel
        };

        return Ok(await _sender.Send(command, cancellationToken));
    }

    /// <summary>Deactivates a product (it stays in the system but is no longer active).</summary>
    [HttpPost("{id:int}/deactivate")]
    [HasPermission(Permissions.Products.Update)]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductDto>> Deactivate(
        int id,
        [FromBody] ProductVersionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SetProductActiveCommand { Id = id, Version = request.Version, IsActive = false };

        return Ok(await _sender.Send(command, cancellationToken));
    }

    /// <summary>Reactivates a previously deactivated product.</summary>
    [HttpPost("{id:int}/activate")]
    [HasPermission(Permissions.Products.Update)]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductDto>> Activate(
        int id,
        [FromBody] ProductVersionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SetProductActiveCommand { Id = id, Version = request.Version, IsActive = true };

        return Ok(await _sender.Send(command, cancellationToken));
    }
}