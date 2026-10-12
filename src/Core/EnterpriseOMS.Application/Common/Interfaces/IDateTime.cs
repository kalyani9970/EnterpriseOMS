using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Common.Interfaces
{
    /// <summary>Abstracts DateTime.UtcNow so handlers/tests can control "now".</summary>
    public interface IDateTime
    {
        DateTime UtcNow { get; }
    }
}
