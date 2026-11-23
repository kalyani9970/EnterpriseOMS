using EnterpriseOMS.Application.IntegrationTests.Support;
using EnterpriseOMS.Domain.Entities.Identity;
using EnterpriseOMS.Domain.Entities.Tenancy;
using EnterpriseOMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.IntegrationTests.Persistence
{
    public class OrganizationPersistenceTests : IntegrationTestBase
    {
        [Fact]
        public async Task DuplicateOrganizationCode_ViolatesTheUniqueIndex()
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            db.Organizations.Add(Organization.Create("First", "dup"));
            db.Organizations.Add(Organization.Create("Second", "dup"));

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        [Fact]
        public async Task User_WithAnOrganizationThatDoesNotExist_IsRejectedByTheForeignKey()
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var employee = await db.Roles.SingleAsync(r => r.Name == "Employee");

            db.Users.Add(new User
            {
                FullName = "Orphan",
                Email = "orphan@example.com",
                NormalizedEmail = "ORPHAN@EXAMPLE.COM",
                PasswordHash = "hash",
                OrganizationId = 9999,
                RoleId = employee.Id
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        [Fact]
        public async Task Organization_ThatStillHasUsers_CannotBeDeleted()
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var defaultOrganization = await db.Organizations.SingleAsync(o => o.Code == "default");

            db.Organizations.Remove(defaultOrganization);   // it holds the seeded admin

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        [Fact]
        public async Task NewOrganization_GetsAuditFieldsStamped()
        {
            ActAs(userId: 42, organizationId: null);
            var createdAt = App.Clock.UtcNow;

            using (var scope = App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Organizations.Add(Organization.Create("Audited", "audited"));
                await db.SaveChangesAsync();
            }

            ActAnonymously();
            using var verifyScope = App.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var organization = await verifyDb.Organizations.SingleAsync(o => o.Code == "audited");

            Assert.Equal(createdAt, organization.Created);
            Assert.Equal("42", organization.CreatedBy);
            Assert.Null(organization.LastModified);
        }
    }
}
