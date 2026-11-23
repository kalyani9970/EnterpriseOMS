using EnterpriseOMS.Application.IntegrationTests.Support;
using EnterpriseOMS.Application.Modules.Auth.DTOs;
using EnterpriseOMS.Domain.Common;
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
    /// <summary>
    /// The most important tests of the multi-tenancy design: whatever a handler forgets to filter,
    /// the database layer must never show one organization another organization's data.
    /// The seeded host has the "default" organization with its admin, and each test adds two more.
    /// </summary>
    public class TenantIsolationTests : IntegrationTestBase
    {
        private async Task<(AuthResultDto Alice, AuthResultDto Bob)> TwoOrganizationsAsync()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            var bob = await RegisterAsync("bob@globex.test", "Globex");
            return (alice, bob);
        }

        private async Task<List<string>> VisibleUserEmailsAsync()
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            return await db.Users.OrderBy(u => u.Email).Select(u => u.Email).ToListAsync();
        }

        private async Task<List<string>> VisibleOrganizationCodesAsync()
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            return await db.Organizations.OrderBy(o => o.Code).Select(o => o.Code).ToListAsync();
        }

        [Fact]
        public async Task SignedInUser_SeesOnlyUsersOfTheirOwnOrganization()
        {
            var (alice, _) = await TwoOrganizationsAsync();
            await ActAsAsync(alice);

            Assert.Equal(new[] { "alice@acme.test" }, await VisibleUserEmailsAsync());
        }

        [Fact]
        public async Task SignedInUser_SeesOnlyTheirOwnOrganization()
        {
            var (alice, _) = await TwoOrganizationsAsync();
            await ActAsAsync(alice);

            Assert.Equal(new[] { "acme-traders" }, await VisibleOrganizationCodesAsync());
        }

        [Fact]
        public async Task UserOfAnotherOrganization_SeesOnlyTheirs()
        {
            var (_, bob) = await TwoOrganizationsAsync();
            await ActAsAsync(bob);

            Assert.Equal(new[] { "bob@globex.test" }, await VisibleUserEmailsAsync());
            Assert.Equal(new[] { "globex" }, await VisibleOrganizationCodesAsync());
        }

        [Fact]
        public async Task Anonymous_SeesEverything_BecauseLoginAndRegisterSpanAllOrganizations()
        {
            await TwoOrganizationsAsync();
            ActAnonymously();

            Assert.Equal(3, (await VisibleUserEmailsAsync()).Count);   // seeded admin, alice, bob
            Assert.Equal(new[] { "acme-traders", "default", "globex" }, await VisibleOrganizationCodesAsync());
        }

        [Fact]
        public async Task SignedInWithoutAnOrganization_SeesNothing()
        {
            await TwoOrganizationsAsync();
            ActAs(userId: 1, organizationId: null);   // a token that somehow has no org_id claim

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            Assert.Equal(0, await db.Users.CountAsync());
            Assert.Equal(0, await db.Organizations.CountAsync());
            Assert.Equal(0, await db.RefreshTokens.CountAsync());
        }

        [Fact]
        public async Task IgnoreQueryFilters_BypassesTheFilter()
        {
            var (alice, _) = await TwoOrganizationsAsync();
            await ActAsAsync(alice);

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            Assert.Equal(1, await db.Users.CountAsync());
            Assert.Equal(3, await db.Users.IgnoreQueryFilters().CountAsync());
        }

        [Fact]
        public async Task RefreshTokens_AreFilteredThroughTheirUsersOrganization()
        {
            var (alice, _) = await TwoOrganizationsAsync();

            using (var scope = App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Assert.Equal(2, await db.RefreshTokens.CountAsync());   // anonymous: alice's and bob's
            }

            await ActAsAsync(alice);

            using var aliceScope = App.Services.CreateScope();
            var aliceDb = aliceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var visibleTokens = await aliceDb.RefreshTokens.ToListAsync();

            Assert.Equal(alice.RefreshToken, Assert.Single(visibleTokens).Token);
        }

        [Fact]
        public async Task UserOfAnotherOrganization_CannotBeLoadedEvenByExactId()
        {
            var (alice, bob) = await TwoOrganizationsAsync();
            await ActAsAsync(alice);

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            Assert.Null(await db.Users.FirstOrDefaultAsync(u => u.Id == bob.UserId));
            Assert.NotNull(await db.Users.FirstOrDefaultAsync(u => u.Id == alice.UserId));
        }

        [Fact]
        public async Task TheFilterFollowsTheCurrentUser_EvenOnTheSameDbContext()
        {
            var (alice, bob) = await TwoOrganizationsAsync();

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await ActAsAsync(alice);
            var asAlice = await db.Users.Select(u => u.Email).ToListAsync();

            await ActAsAsync(bob);
            var asBob = await db.Users.Select(u => u.Email).ToListAsync();

            Assert.Equal(new[] { "alice@acme.test" }, asAlice);
            Assert.Equal(new[] { "bob@globex.test" }, asBob);
        }

        [Fact]
        public void EveryTenantEntity_HasAQueryFilter()
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Any class implementing ITenantEntity, now or added by a future module. If someone adds
            // one and its filter is somehow missing, this test fails instead of data leaking.
            var tenantTypes = typeof(ITenantEntity).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(ITenantEntity).IsAssignableFrom(t))
                .ToList();

            Assert.NotEmpty(tenantTypes);

            foreach (var type in tenantTypes)
            {
                var entityType = db.Model.FindEntityType(type);

                Assert.NotNull(entityType);
                Assert.NotNull(entityType!.GetQueryFilter());
            }

            // The two entities that are scoped to a tenant without implementing the interface.
            Assert.NotNull(db.Model.FindEntityType(typeof(Organization))!.GetQueryFilter());
            Assert.NotNull(db.Model.FindEntityType(typeof(RefreshToken))!.GetQueryFilter());
        }
    }
}
