using EnterpriseOMS.Domain.Entities.Catalog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Inventory.DTOs
{
    /// <summary>One row of the stock overview: how much of a product is in a warehouse, and whether it is running low.</summary>
    public class StockLevelListItemDto
    {
        public int ProductId { get; set; }
        public string ProductSku { get; set; } = default!;
        public string ProductName { get; set; } = default!;
        public UnitOfMeasure UnitOfMeasure { get; set; }
        public int WarehouseId { get; set; }
        public string WarehouseCode { get; set; } = default!;
        public string WarehouseName { get; set; } = default!;
        public int QuantityOnHand { get; set; }
        public int ReorderLevel { get; set; }

        /// <summary>True when the quantity on hand has reached the product's reorder level (or below it).</summary>
        public bool IsLowStock { get; set; }
    }
}
