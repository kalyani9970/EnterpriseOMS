using EnterpriseOMS.Domain.Entities.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.UnitTests.Entities
{
    public class RoleTests
    {
        // Role.AssignPermission compares by PermissionId, so test permissions need distinct Ids,
        // just like persisted permissions would have.
        private static Permission CreatePermission(int id, string code) => new()
        {
            Id = id,
            Code = code,
            Module = "Test",
            Description = $"Permission '{code}'"
        };

        [Fact]
        public void AssignPermission_AddsLinkBetweenRoleAndPermission()
        {
            var role = new Role { Id = 1, Name = "Admin" };
            var permission = CreatePermission(10, "users.view");

            role.AssignPermission(permission);

            var link = Assert.Single(role.RolePermissions);
            Assert.Equal(1, link.RoleId);
            Assert.Equal(10, link.PermissionId);
            Assert.Same(role, link.Role);
            Assert.Same(permission, link.Permission);
        }

        [Fact]
        public void AssignPermission_SamePermissionTwice_IsIdempotent()
        {
            var role = new Role { Id = 1, Name = "Admin" };
            var permission = CreatePermission(10, "users.view");

            role.AssignPermission(permission);
            role.AssignPermission(permission);

            Assert.Single(role.RolePermissions);
        }

        [Fact]
        public void AssignPermission_DifferentPermissions_AddsEach()
        {
            var role = new Role { Id = 1, Name = "Admin" };

            role.AssignPermission(CreatePermission(10, "users.view"));
            role.AssignPermission(CreatePermission(11, "users.create"));

            Assert.Equal(2, role.RolePermissions.Count);
        }

        [Fact]
        public void RevokePermission_RemovesTheLink()
        {
            var role = new Role { Id = 1, Name = "Admin" };
            role.AssignPermission(CreatePermission(10, "users.view"));
            role.AssignPermission(CreatePermission(11, "users.create"));

            role.RevokePermission(10);

            var remaining = Assert.Single(role.RolePermissions);
            Assert.Equal(11, remaining.PermissionId);
        }

        [Fact]
        public void RevokePermission_UnknownPermission_DoesNothing()
        {
            var role = new Role { Id = 1, Name = "Admin" };
            role.AssignPermission(CreatePermission(10, "users.view"));

            role.RevokePermission(999);

            Assert.Single(role.RolePermissions);
        }
    }
}
