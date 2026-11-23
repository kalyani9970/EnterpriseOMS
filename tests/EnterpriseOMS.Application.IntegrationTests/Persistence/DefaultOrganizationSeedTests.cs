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
    public class DefaultOrganizationSeedTests : IntegrationTestBase
    {
        [Fact]
        public async Task Seed_CreatesTheDefaultOrganization()
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var organization = await db.Organizations.SingleAsync(o => o.Code == IdentitySeeder.DefaultOrganizationCode);

            Assert.Equal(IdentitySeeder.DefaultOrganizationName, organization.Name);
            Assert.True(organization.IsActive);
        }

        [Fact]
        public async Task Seed_PlacesTheBootstrapAdminInTheDefaultOrganization()
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var organization = await db.Organizations.SingleAsync(o => o.Code == IdentitySeeder.DefaultOrganizationCode);
            var admin = await db.Users.SingleAsync(u => u.Email == TestHost.AdminEmail);

            Assert.Equal(organization.Id, admin.OrganizationId);
        }

        [Fact]
        public async Task Seed_RunTwice_KeepsExactlyOneDefaultOrganization()
        {
            using (var scope = App.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();
            }

            using var verifyScope = App.Services.CreateScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            Assert.Equal(1, await db.Organizations.CountAsync());
            Assert.Equal(1, await db.Users.CountAsync(u => u.Email == TestHost.AdminEmail));
        }

        [Fact]
        public async Task Seed_GrantsTheOrganizationPermissionsToAdminOnly()
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var adminCodes = await db.Roles
                .Where(r => r.Name == "Admin")
                .SelectMany(r => r.RolePermissions)
                .Select(rp => rp.Permission.Code)
                .ToListAsync();

            var employeeCodes = await db.Roles
                .Where(r => r.Name == "Employee")
                .SelectMany(r => r.RolePermissions)
                .Select(rp => rp.Permission.Code)
                .ToListAsync();

            Assert.Contains(adminCodes, code => code == "organizations.view");
            Assert.Contains(adminCodes, code => code == "organizations.update");
            Assert.Empty(employeeCodes);
        }
    }
}
