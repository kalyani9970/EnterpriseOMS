using EnterpriseOMS.Application.Modules.Inventory.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Inventory.Commands.AdjustStock
{
    /// <summary>
    /// A stock count. You say what you counted; the system records the difference, either sign, together with
    /// the reason. The reason is required: an unexplained correction is exactly what an audit trail is for.
    /// </summary>
    public class AdjustStockCommand : IRequest<StockMovementDto>
    {
        public int ProductId { get; set; }
        public int WarehouseId { get; set; }
        public int CountedQuantity { get; set; }
        public string Reason { get; set; } = default!;
    }
}
