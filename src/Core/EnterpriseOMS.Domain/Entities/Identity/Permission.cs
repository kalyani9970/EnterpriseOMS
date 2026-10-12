using EnterpriseOMS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.Entities.Identity
{
    /// <summary>
    /// A granular capability, e.g. "orders.create", "inventory.adjust".
    /// Naming convention: "{module}.{action}" — all lowercase, dot-separated.
    /// This is what gets embedded as a claim in the JWT and checked by
    /// PermissionAuthorizationHandler.
    /// </summary>
    public class Permission : BaseEntity
    {
        public string Code { get; set; } = default!;      // e.g. "orders.create"
        public string Module { get; set; } = default!;    // e.g. "Orders"
        public string Description { get; set; } = default!;

        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}
