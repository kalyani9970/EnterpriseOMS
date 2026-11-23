using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.Common
{
    /// <summary>
    /// Marks an entity as belonging to exactly one organization (tenant). The persistence layer
    /// attaches a global query filter to every ITenantEntity, so a signed-in user can only ever
    /// read rows from their own organization. Every future business entity (products, warehouses,
    /// orders...) implements this interface.
    /// </summary>
    public interface ITenantEntity
    {
        int OrganizationId { get; }
    }
}
