using EnterpriseOMS.Domain.Entities.Catalog;
using EnterpriseOMS.Domain.Entities.Inventory;
using EnterpriseOMS.Domain.Entities.Warehousing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Inventory.DTOs
{
    /// <summary>One line of the stock ledger, with the product and warehouse spelled out so a screen needs no extra lookups.</summary>
    public class StockMovementDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductSku { get; set; } = default!;
        public string ProductName { get; set; } = default!;
        public int WarehouseId { get; set; }
        public string WarehouseCode { get; set; } = default!;
        public string WarehouseName { get; set; } = default!;
        public StockMovementType Type { get; set; }

        /// <summary>Signed: positive for stock in, negative for stock out.</summary>
        public int QuantityChange { get; set; }

        /// <summary>The stock level right after this movement.</summary>
        public int QuantityAfter { get; set; }

        public string? Reference { get; set; }
        public string? Notes { get; set; }
        public DateTime Created { get; set; }
        public string? CreatedBy { get; set; }

        public static StockMovementDto From(StockMovement movement, Product product, Warehouse warehouse) => new()
        {
            Id = movement.Id,
            ProductId = product.Id,
            ProductSku = product.Sku,
            ProductName = product.Name,
            WarehouseId = warehouse.Id,
            WarehouseCode = warehouse.Code,
            WarehouseName = warehouse.Name,
            Type = movement.Type,
            QuantityChange = movement.QuantityChange,
            QuantityAfter = movement.QuantityAfter,
            Reference = movement.Reference,
            Notes = movement.Notes,
            Created = movement.Created,
            CreatedBy = movement.CreatedBy
        };
    }
}
