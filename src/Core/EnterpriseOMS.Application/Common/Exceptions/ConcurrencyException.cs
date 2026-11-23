using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Common.Exceptions
{
    /// <summary>
    /// The record was changed by someone else since the caller loaded it. Mapped to HTTP 409 so the client
    /// can reload and let the user reapply their edit, instead of overwriting the other person's work.
    /// </summary>
    public class ConcurrencyException : Exception
    {
        public ConcurrencyException(string entityName, object key)
        : base($"{entityName} ({key}) was changed by someone else. Reload it and try again.")
        {
        }
    }
}
