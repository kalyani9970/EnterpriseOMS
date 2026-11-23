using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Common.Interfaces
{
    /// <summary>
    /// Runs a block of work as ONE database transaction: everything inside commits together, or, if
    /// anything throws, none of it does. If a transaction is already open, the block simply joins it.
    /// </summary>
    public interface IUnitOfWork
    {
        Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken);
    }
}
