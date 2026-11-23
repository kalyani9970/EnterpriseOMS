using EnterpriseOMS.Application.Common.Models;
using EnterpriseOMS.Application.Modules.Inventory.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Inventory.Queries.GetStockLevels
{
    /// <summary>One page of the stock overview. Bound from the query string; every property has a safe default.</summary>
    public class GetStockLevelsQuery : IRequest<PaginatedList<StockLevelListItemDto>>
    {
        public const int MaxPageSize = 100;

        /// <summary>The fields the overview can be sorted by. Anything else is rejected, never passed to the database.</summary>
        public static readonly string[] SortFields = { "sku", "name", "quantity", "warehouse" };

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;

        public int? WarehouseId { get; set; }
        public int? ProductId { get; set; }

        /// <summary>Matches part of the product's SKU or name, ignoring case.</summary>
        public string? Search { get; set; }

        /// <summary>true = only rows whose quantity has reached the product's reorder level.</summary>
        public bool LowStockOnly { get; set; }

        public string SortBy { get; set; } = "sku";
        public bool Descending { get; set; }
    }
}
