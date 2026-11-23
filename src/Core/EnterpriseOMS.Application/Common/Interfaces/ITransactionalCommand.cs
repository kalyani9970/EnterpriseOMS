using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Common.Interfaces
{
    /// <summary>
    /// Marker for commands whose handler makes SEVERAL changes that must succeed or fail together
    /// (for example several SaveChanges calls, or stock moving between warehouses). The pipeline's
    /// TransactionBehavior wraps these, and only these, in a database transaction. A handler with a
    /// single SaveChanges is already atomic and does not need the marker.
    /// </summary>
    public interface ITransactionalCommand
    {
    }
}
