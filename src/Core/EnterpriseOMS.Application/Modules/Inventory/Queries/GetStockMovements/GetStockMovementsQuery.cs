using EnterpriseOMS.Application.Common.Models;
using EnterpriseOMS.Application.Modules.Inventory.DTOs;
using EnterpriseOMS.Domain.Entities.Inventory;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Inventory.Queries.GetStockMovements
{
    /// <summary>One page of the stock ledger, newest first. Bound from the query string.</summary>
    public class GetStockMovementsQuery : IRequest<PaginatedList<StockMovementDto>>
    {
        public const int MaxPageSize = 100;

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;

        public int? ProductId { get; set; }
        public int? WarehouseId { get; set; }

        /// <summary>Only receipts, only issues, or only adjustments. null = every kind.</summary>
        public StockMovementType? Type { get; set; }
    }
}
