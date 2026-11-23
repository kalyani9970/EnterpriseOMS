using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.Entities.Inventory
{
    /// <summary>
    /// Why stock changed. Later modules add their own kinds (transfers, sales and purchase receipts);
    /// for now stock is received, issued, or corrected after a count.
    /// </summary>
    public enum StockMovementType
    {
        /// <summary>Stock came in (a delivery, a return to stock). The change is positive.</summary>
        Receipt = 1,

        /// <summary>Stock went out (used, shipped, written off). The change is negative.</summary>
        Issue = 2,

        /// <summary>A stock count found a different number than the system said. The change is either sign.</summary>
        Adjustment = 3
    }
}
