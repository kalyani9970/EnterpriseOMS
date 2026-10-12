using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.Common.Exceptions
{
    /// <summary>
    /// Thrown when a domain invariant is violated (e.g., negative stock,
    /// invalid state transition). Distinct from validation exceptions,
    /// which catch bad input shape before it reaches the domain.
    /// </summary>
    public class DomainException : Exception
    {
        public DomainException(string message) : base(message) { }
    }
}
