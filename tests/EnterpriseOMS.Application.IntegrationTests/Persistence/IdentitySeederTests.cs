using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Application.Common.Security;
using EnterpriseOMS.Application.IntegrationTests.Support;
using EnterpriseOMS.Infrastructure.Persistence;
using EnterpriseOMS.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.IntegrationTests.Persistence
{
    public class IdentitySeederTests : IntegrationTestBase
    {
        [Fact]
        public async Task Seed_CreatesPermissionsFromConstantsClass()
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var codes = await db.Permissions.Select(p => p.Code).ToListAsync();

            Assert.Contains(Permissions.Users.View, codes);
            Assert.Contains(Permissions.Roles.ManagePermissions, codes);
            Assert.Equal(codes.Count, codes.Distinct().Count());
        }

        [Fact]
        public async Task Seed_PermissionModuleIsTheNestedClassName()
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var permission = await db.Permissions.SingleAsync(p => p.Code == Permissions.Roles.View);

            Assert.Equal("Roles", permission.Module);
        }

        [Fact]
        public async Task Seed_AdminRoleHoldsEveryPermission()
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var admin = await db.Roles
                .Include(r => r.RolePermissions)
                .SingleAsync(r => r.Name == IdentitySeeder.AdminRoleName);
            var totalPermissions = await db.Permissions.CountAsync();

            Assert.True(admin.IsSystemRole);
            Assert.Equal(totalPermissions, admin.RolePermissions.Count);
        }

        [Fact]
        public async Task Seed_EmployeeRoleExistsWithNoPermissions()
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var employee = await db.Roles
                .Include(r => r.RolePermissions)
                .SingleAsync(r => r.Name == IdentitySeeder.EmployeeRoleName);

            Assert.True(employee.IsSystemRole);
            Assert.Empty(employee.RolePermissions);
        }

        [Fact]
        public async Task Seed_CreatesBootstrapAdminUser()
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            var admin = await db.Users
                .Include(u => u.Role)
                .SingleAsync(u => u.Email == TestHost.AdminEmail);

            Assert.Equal("Admin", admin.Role.Name);
            Assert.True(admin.IsActive);
            Assert.True(hasher.Verify(admin.PasswordHash, TestHost.AdminPassword));
        }

        [Fact]
        public async Task Seed_RunTwice_ChangesNothing()
        {
            int permissions, roles, users, rolePermissions;

            using (var scope = App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                permissions = await db.Permissions.CountAsync();
                roles = await db.Roles.CountAsync();
                users = await db.Users.CountAsync();
                rolePermissions = await db.RolePermissions.CountAsync();
            }

            using (var scope = App.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();
            }

            using (var scope = App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Assert.Equal(permissions, await db.Permissions.CountAsync());
                Assert.Equal(roles, await db.Roles.CountAsync());
                Assert.Equal(users, await db.Users.CountAsync());
                Assert.Equal(rolePermissions, await db.RolePermissions.CountAsync());
            }
        }
    }
}
