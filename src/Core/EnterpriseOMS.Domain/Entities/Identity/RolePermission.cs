using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.Entities.Identity
{
    /// <summary>Join entity — explicit (not implicit many-to-many) so we can
    /// later add metadata like GrantedBy/GrantedOn without migration pain.</summary>
    public class RolePermission
    {
        public int RoleId { get; set; }
        public Role Role { get; set; } = default!;

        public int PermissionId { get; set; }
        public Permission Permission { get; set; } = default!;
    }
}
