using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Application.Common.Models;
using EnterpriseOMS.Application.Common.Querying;
using EnterpriseOMS.Application.Modules.Inventory.DTOs;
using EnterpriseOMS.Domain.Entities.Catalog;
using EnterpriseOMS.Domain.Entities.Inventory;
using EnterpriseOMS.Domain.Entities.Warehousing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Inventory.Queries.GetStockLevels
{
    public class GetStockLevelsQueryHandler : IRequestHandler<GetStockLevelsQuery, PaginatedList<StockLevelListItemDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetStockLevelsQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PaginatedList<StockLevelListItemDto>> Handle(
            GetStockLevelsQuery request,
            CancellationToken cancellationToken)
        {
            // A JOIN of the stock level with its product and warehouse: one SQL statement, no extra round trip
            // per row. All three tables are tenant-filtered, so only the caller's organization appears.
            IQueryable<Row> query =
                from level in _context.StockLevels.AsNoTracking()
                join product in _context.Products on level.ProductId equals product.Id
                join warehouse in _context.Warehouses on level.WarehouseId equals warehouse.Id
                select new Row { Level = level, Product = product, Warehouse = warehouse };

            if (request.WarehouseId.HasValue)
            {
                var warehouseId = request.WarehouseId.Value;
                query = query.Where(row => row.Level.WarehouseId == warehouseId);
            }

            if (request.ProductId.HasValue)
            {
                var productId = request.ProductId.Value;
                query = query.Where(row => row.Level.ProductId == productId);
            }

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var pattern = LikeSearch.ContainsPattern(request.Search);
                query = query.Where(row =>
                    EF.Functions.Like(row.Product.Sku, pattern, LikeSearch.EscapeCharacter) ||
                    EF.Functions.Like(row.Product.Name, pattern, LikeSearch.EscapeCharacter));
            }

            if (request.LowStockOnly)
            {
                query = query.Where(row => row.Level.QuantityOnHand <= row.Product.ReorderLevel);
            }

            query = ApplySort(query, request.SortBy, request.Descending);

            var items = query.Select(row => new StockLevelListItemDto
            {
                ProductId = row.Product.Id,
                ProductSku = row.Product.Sku,
                ProductName = row.Product.Name,
                UnitOfMeasure = row.Product.UnitOfMeasure,
                WarehouseId = row.Warehouse.Id,
                WarehouseCode = row.Warehouse.Code,
                WarehouseName = row.Warehouse.Name,
                QuantityOnHand = row.Level.QuantityOnHand,
                ReorderLevel = row.Product.ReorderLevel,
                IsLowStock = row.Level.QuantityOnHand <= row.Product.ReorderLevel
            });

            return await PaginatedList<StockLevelListItemDto>.CreateAsync(items, request.PageNumber, request.PageSize);
        }

        /// <summary>
        /// Whitelist-based sorting. Every ordering ends with the warehouse and then the level's Id, so equal
        /// values still page in a stable, repeatable order.
        /// </summary>
        private static IQueryable<Row> ApplySort(IQueryable<Row> query, string sortBy, bool descending)
        {
            return (sortBy.ToLowerInvariant(), descending) switch
            {
                ("name", false) => query.OrderBy(r => r.Product.Name).ThenBy(r => r.Warehouse.NormalizedCode).ThenBy(r => r.Level.Id),
                ("name", true) => query.OrderByDescending(r => r.Product.Name).ThenBy(r => r.Warehouse.NormalizedCode).ThenBy(r => r.Level.Id),

                ("quantity", false) => query.OrderBy(r => r.Level.QuantityOnHand).ThenBy(r => r.Product.NormalizedSku).ThenBy(r => r.Level.Id),
                ("quantity", true) => query.OrderByDescending(r => r.Level.QuantityOnHand).ThenBy(r => r.Product.NormalizedSku).ThenBy(r => r.Level.Id),

                ("warehouse", false) => query.OrderBy(r => r.Warehouse.NormalizedCode).ThenBy(r => r.Product.NormalizedSku).ThenBy(r => r.Level.Id),
                ("warehouse", true) => query.OrderByDescending(r => r.Warehouse.NormalizedCode).ThenBy(r => r.Product.NormalizedSku).ThenBy(r => r.Level.Id),

                (_, false) => query.OrderBy(r => r.Product.NormalizedSku).ThenBy(r => r.Warehouse.NormalizedCode).ThenBy(r => r.Level.Id),
                (_, true) => query.OrderByDescending(r => r.Product.NormalizedSku).ThenBy(r => r.Warehouse.NormalizedCode).ThenBy(r => r.Level.Id)
            };
        }

        /// <summary>The joined row: a level with its product and warehouse. Only used inside this handler's query.</summary>
        private sealed class Row
        {
            public StockLevel Level { get; set; } = default!;
            public Product Product { get; set; } = default!;
            public Warehouse Warehouse { get; set; } = default!;
        }
    }
}
