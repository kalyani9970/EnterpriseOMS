using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.Entities.Sales
{
    /// <summary>
    /// Where an order is in its life. Draft: being put together, no effect on stock. Confirmed: the stock is
    /// RESERVED for it. Fulfilled: the stock has left the warehouse. Cancelled: it will not happen (any
    /// reservation was given back). The numbers are stored and sent, so they never change.
    /// </summary>
    public enum OrderStatus
    {
        Draft = 1,
        Confirmed = 2,
        Fulfilled = 3,
        Cancelled = 4
    }
}
