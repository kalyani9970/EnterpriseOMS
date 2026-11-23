using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Application.Common.Querying;
using EnterpriseOMS.Application.Common.Models;
using EnterpriseOMS.Application.Modules.Products.DTOs;
using EnterpriseOMS.Domain.Entities.Catalog;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseOMS.Application.Modules.Products.Queries.GetProductsList;

public class GetProductsListQueryHandler : IRequestHandler<GetProductsListQuery, PaginatedList<ProductListItemDto>>
{
    private readonly IApplicationDbContext _context;

    public GetProductsListQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<ProductListItemDto>> Handle(
        GetProductsListQuery request,
        CancellationToken cancellationToken)
    {
        // Build the query step by step. Nothing touches the database until the end, so EF combines
        // every Where/OrderBy/Skip/Take into ONE SQL statement. The tenant filter is already part of it.
        IQueryable<Product> query = _context.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // LIKE with an explicit escape character: a user typing "50%" or "a_b" searches for
            // those literal characters instead of acting as wildcards (see LikeSearch).
            var pattern = LikeSearch.ContainsPattern(request.Search);
            query = query.Where(p =>
                EF.Functions.Like(p.Sku, pattern, LikeSearch.EscapeCharacter) ||
                EF.Functions.Like(p.Name, pattern, LikeSearch.EscapeCharacter));
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var category = request.Category.Trim();
            query = query.Where(p => p.Category == category);
        }

        if (request.IsActive.HasValue)
        {
            var isActive = request.IsActive.Value;
            query = query.Where(p => p.IsActive == isActive);
        }

        query = ApplySort(query, request.SortBy, request.Descending);

        var items = query.Select(p => new ProductListItemDto
        {
            Id = p.Id,
            Sku = p.Sku,
            Name = p.Name,
            Category = p.Category,
            UnitOfMeasure = p.UnitOfMeasure,
            UnitPrice = p.UnitPrice,
            ReorderLevel = p.ReorderLevel,
            IsActive = p.IsActive,
            Version = p.Version
        });

        return await PaginatedList<ProductListItemDto>.CreateAsync(items, request.PageNumber, request.PageSize);
    }

    /// <summary>
    /// Whitelist-based sorting: the caller picks from a fixed set, never an arbitrary column.
    /// Every ordering ends with Id so that equal values still page in a stable, repeatable order.
    /// </summary>
    private static IQueryable<Product> ApplySort(IQueryable<Product> query, string sortBy, bool descending)
    {
        return (sortBy.ToLowerInvariant(), descending) switch
        {
            ("sku", false) => query.OrderBy(p => p.NormalizedSku).ThenBy(p => p.Id),
            ("sku", true) => query.OrderByDescending(p => p.NormalizedSku).ThenBy(p => p.Id),

            // Cast to double only for ORDERING: SQLite cannot order by decimal, and a double is exact
            // enough to order prices with 2 decimals. The price shown to users is still the decimal.
            ("price", false) => query.OrderBy(p => (double)p.UnitPrice).ThenBy(p => p.Id),
            ("price", true) => query.OrderByDescending(p => (double)p.UnitPrice).ThenBy(p => p.Id),

            ("created", false) => query.OrderBy(p => p.Created).ThenBy(p => p.Id),
            ("created", true) => query.OrderByDescending(p => p.Created).ThenBy(p => p.Id),

            (_, false) => query.OrderBy(p => p.Name).ThenBy(p => p.Id),
            (_, true) => query.OrderByDescending(p => p.Name).ThenBy(p => p.Id)
        };
    }
}