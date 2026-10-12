using EnterpriseOMS.Application.IntegrationTests.Support;
using EnterpriseOMS.Domain.Common;
using EnterpriseOMS.Domain.Entities.Identity;
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
    public class ApplicationDbContextTests : IntegrationTestBase
    {
        [Fact]
        public async Task SaveChanges_OnAdd_StampsCreatedAndCreatedBy()
        {
            App.CurrentUser.UserId = 42;
            var expectedCreated = App.Clock.UtcNow;

            using (var scope = App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Roles.Add(new Role { Name = "Auditor" });
                await db.SaveChangesAsync();
            }

            using var verifyScope = App.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var role = await verifyDb.Roles.SingleAsync(r => r.Name == "Auditor");

            Assert.Equal(expectedCreated, role.Created);
            Assert.Equal("42", role.CreatedBy);
            Assert.Null(role.LastModified);
        }

        [Fact]
        public async Task SaveChanges_OnUpdate_StampsLastModifiedAndKeepsCreated()
        {
            App.CurrentUser.UserId = 42;
            var createdAt = App.Clock.UtcNow;

            using (var scope = App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Roles.Add(new Role { Name = "Auditor" });
                await db.SaveChangesAsync();
            }

            App.Clock.Advance(TimeSpan.FromMinutes(5));
            App.CurrentUser.UserId = 99;
            var modifiedAt = App.Clock.UtcNow;

            using (var scope = App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var role = await db.Roles.SingleAsync(r => r.Name == "Auditor");
                role.Description = "Reads audit logs";
                await db.SaveChangesAsync();
            }

            using var verifyScope = App.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var reloaded = await verifyDb.Roles.SingleAsync(r => r.Name == "Auditor");

            Assert.Equal(createdAt, reloaded.Created);
            Assert.Equal("42", reloaded.CreatedBy);
            Assert.Equal(modifiedAt, reloaded.LastModified);
            Assert.Equal("99", reloaded.LastModifiedBy);
        }

        [Fact]
        public async Task SaveChanges_DuplicateNormalizedEmail_ViolatesUniqueIndex()
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var employee = await db.Roles.SingleAsync(r => r.Name == "Employee");

            User NewUser(string email) => new()
            {
                FullName = "Dup",
                Email = email,
                NormalizedEmail = "DUP@EXAMPLE.COM",
                PasswordHash = "hash",
                OrganizationId = 1,
                RoleId = employee.Id
            };

            db.Users.Add(NewUser("dup@example.com"));
            db.Users.Add(NewUser("DUP@example.com"));

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        [Fact]
        public async Task Role_WithUsers_CannotBeDeleted()
        {
            // The User → Role relationship is Restrict, so a role in use cannot be removed.
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var admin = await db.Roles.SingleAsync(r => r.Name == "Admin");

            db.Roles.Remove(admin);

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        [Fact]
        public void Model_DoesNotPersistDomainEvents()
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var userType = db.Model.FindEntityType(typeof(User))!;

            Assert.Null(db.Model.FindEntityType(typeof(BaseDomainEvent)));
            Assert.Null(userType.FindProperty(nameof(User.DomainEvents)));
            Assert.Null(userType.FindNavigation(nameof(User.DomainEvents)));
        }
    }
}
