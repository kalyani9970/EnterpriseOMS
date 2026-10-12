using EnterpriseOMS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.Entities.Identity
{
    public class Role : BaseAuditableEntity, IAggregateRoot
    {
        public string Name { get; set; } = default!;       // e.g. "Admin", "WarehouseManager"
        public string? Description { get; set; }
        public bool IsSystemRole { get; set; }              // system roles cannot be deleted

        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
        public ICollection<User> Users { get; set; } = new List<User>();

        public void AssignPermission(Permission permission)
        {
            if (RolePermissions.Any(rp => rp.PermissionId == permission.Id))
                return;

            RolePermissions.Add(new RolePermission { RoleId = Id, PermissionId = permission.Id, Role = this, Permission = permission });
        }

        public void RevokePermission(int permissionId)
        {
            var rp = RolePermissions.FirstOrDefault(x => x.PermissionId == permissionId);
            if (rp != null) RolePermissions.Remove(rp);
        }
    }
}
