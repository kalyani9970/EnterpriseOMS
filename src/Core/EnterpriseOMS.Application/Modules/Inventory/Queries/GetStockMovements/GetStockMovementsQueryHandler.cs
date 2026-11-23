using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Application.Common.Models;
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

namespace EnterpriseOMS.Application.Modules.Inventory.Queries.GetStockMovements
{
    public class GetStockMovementsQueryHandler : IRequestHandler<GetStockMovementsQuery, PaginatedList<StockMovementDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetStockMovementsQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PaginatedList<StockMovementDto>> Handle(
            GetStockMovementsQuery request,
            CancellationToken cancellationToken)
        {
            IQueryable<Row> query =
                from movement in _context.StockMovements.AsNoTracking()
                join product in _context.Products on movement.ProductId equals product.Id
                join warehouse in _context.Warehouses on movement.WarehouseId equals warehouse.Id
                select new Row { Movement = movement, Product = product, Warehouse = warehouse };

            if (request.ProductId.HasValue)
            {
                var productId = request.ProductId.Value;
                query = query.Where(row => row.Movement.ProductId == productId);
            }

            if (request.WarehouseId.HasValue)
            {
                var warehouseId = request.WarehouseId.Value;
                query = query.Where(row => row.Movement.WarehouseId == warehouseId);
            }

            if (request.Type.HasValue)
            {
                var type = request.Type.Value;
                query = query.Where(row => row.Movement.Type == type);
            }

            // Newest first. Id breaks ties, so lines recorded in the same instant keep their recording order.
            query = query
                .OrderByDescending(row => row.Movement.Created)
                .ThenByDescending(row => row.Movement.Id);

            var items = query.Select(row => new StockMovementDto
            {
                Id = row.Movement.Id,
                ProductId = row.Product.Id,
                ProductSku = row.Product.Sku,
                ProductName = row.Product.Name,
                WarehouseId = row.Warehouse.Id,
                WarehouseCode = row.Warehouse.Code,
                WarehouseName = row.Warehouse.Name,
                Type = row.Movement.Type,
                QuantityChange = row.Movement.QuantityChange,
                QuantityAfter = row.Movement.QuantityAfter,
                Reference = row.Movement.Reference,
                Notes = row.Movement.Notes,
                Created = row.Movement.Created,
                CreatedBy = row.Movement.CreatedBy
            });

            return await PaginatedList<StockMovementDto>.CreateAsync(items, request.PageNumber, request.PageSize);
        }

        /// <summary>The joined row: a ledger line with its product and warehouse. Only used inside this handler's query.</summary>
        private sealed class Row
        {
            public StockMovement Movement { get; set; } = default!;
            public Product Product { get; set; } = default!;
            public Warehouse Warehouse { get; set; } = default!;
        }
    }
}
