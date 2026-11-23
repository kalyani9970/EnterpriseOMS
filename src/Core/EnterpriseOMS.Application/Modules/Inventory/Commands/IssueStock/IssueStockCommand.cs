using EnterpriseOMS.Application.Modules.Inventory.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Inventory.Commands.IssueStock
{
    /// <summary>Stock went out: used, shipped, or written off.</summary>
    public class IssueStockCommand : IRequest<StockMovementDto>
    {
        public int ProductId { get; set; }
        public int WarehouseId { get; set; }
        public int Quantity { get; set; }

        /// <summary>An outside document number, such as a sales order. Optional.</summary>
        public string? Reference { get; set; }

        public string? Notes { get; set; }
    }
}
