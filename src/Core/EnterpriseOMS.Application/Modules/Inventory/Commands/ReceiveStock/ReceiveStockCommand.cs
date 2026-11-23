using EnterpriseOMS.Application.Modules.Inventory.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Inventory.Commands.ReceiveStock
{
    /// <summary>Stock came in: a delivery, or a return to stock.</summary>
    public class ReceiveStockCommand : IRequest<StockMovementDto>
    {
        public int ProductId { get; set; }
        public int WarehouseId { get; set; }
        public int Quantity { get; set; }

        /// <summary>An outside document number, such as a purchase order. Optional.</summary>
        public string? Reference { get; set; }

        public string? Notes { get; set; }
    }
}
