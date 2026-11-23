using EnterpriseOMS.Application.Common.Exceptions;
using EnterpriseOMS.Application.IntegrationTests.Support;
using EnterpriseOMS.Application.Modules.Products.DTOs;
using EnterpriseOMS.Domain.Entities.Catalog;
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
    /// Products are the first real business data, so these tests matter most: whatever a handler forgets,
    /// the database layer must keep one organization's products away from another's. Both directions:
    /// reading (query filter) and writing (the SaveChanges guard).
    /// </summary>
    public class ProductTenantIsolationTests : IntegrationTestBase
    {
        private async Task<(ProductDto Alice, ProductDto Bob, int AliceOrg, int BobOrg)> TwoOrganizationsWithAProductAsync()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            var bob = await RegisterAsync("bob@globex.test", "Globex");

            await ActAsAsync(alice);
            var alicesProduct = await CreateProductAsync("ALICE-1", name: "Alice's Item");

            await ActAsAsync(bob);
            var bobsProduct = await CreateProductAsync("BOB-1", name: "Bob's Item");

            return (
                alicesProduct,
                bobsProduct,
                await OrganizationIdOfAsync(alice.UserId),
                await OrganizationIdOfAsync(bob.UserId));
        }

        private static Product SneakyProduct(int organizationId) =>
            Product.Create(organizationId, "SNEAKY-1", "Sneaky", null, null, UnitOfMeasure.Piece, 1m, 1m, 0);

        private async Task<int> TotalProductsAsync()
        {
            ActAnonymously();
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            return await db.Products.CountAsync();
        }

        [Fact]
        public async Task Bob_CannotSeeAlicesProducts()
        {
            var (alice, _, _, _) = await TwoOrganizationsWithAProductAsync();   // currently signed in as Bob

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var visible = await db.Products.Select(p => p.Sku).ToListAsync();

            Assert.Equal(new[] { "BOB-1" }, visible);
            Assert.Null(await db.Products.FirstOrDefaultAsync(p => p.Id == alice.Id));
        }

        [Fact]
        public async Task Anonymous_SeesProductsOfEveryOrganization()
        {
            await TwoOrganizationsWithAProductAsync();

            Assert.Equal(2, await TotalProductsAsync());
        }

        [Fact]
        public async Task IgnoreQueryFilters_BypassesTheProductFilter()
        {
            await TwoOrganizationsWithAProductAsync();   // signed in as Bob

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            Assert.Equal(1, await db.Products.CountAsync());
            Assert.Equal(2, await db.Products.IgnoreQueryFilters().CountAsync());
        }

        [Fact]
        public async Task TheFilterFollowsTheCurrentUser_EvenOnTheSameDbContext()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            var bob = await RegisterAsync("bob@globex.test", "Globex");
            await ActAsAsync(alice);
            await CreateProductAsync("ALICE-1");
            await ActAsAsync(bob);
            await CreateProductAsync("BOB-1");

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await ActAsAsync(alice);
            var asAlice = await db.Products.Select(p => p.Sku).ToListAsync();

            await ActAsAsync(bob);
            var asBob = await db.Products.Select(p => p.Sku).ToListAsync();

            Assert.Equal(new[] { "ALICE-1" }, asAlice);
            Assert.Equal(new[] { "BOB-1" }, asBob);
        }

        [Fact]
        public async Task ASignedInUser_CannotSaveAProductForAnotherOrganization()
        {
            var (_, _, aliceOrg, _) = await TwoOrganizationsWithAProductAsync();   // signed in as Bob

            using (var scope = App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Products.Add(SneakyProduct(aliceOrg));   // Bob tries to plant a product in Alice's organization

                await Assert.ThrowsAsync<ForbiddenAccessException>(() => db.SaveChangesAsync());
            }

            Assert.Equal(2, await TotalProductsAsync());   // nothing was saved
        }

        [Fact]
        public async Task ASignedInUser_CannotMoveAProductToAnotherOrganization()
        {
            var (_, bobsProduct, aliceOrg, _) = await TwoOrganizationsWithAProductAsync();   // signed in as Bob

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var product = await db.Products.SingleAsync(p => p.Id == bobsProduct.Id);

            db.Entry(product).Property(nameof(Product.OrganizationId)).CurrentValue = aliceOrg;

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => db.SaveChangesAsync());
        }

        [Fact]
        public async Task ASignedInUserWithoutAnOrganization_CannotSaveAnyProduct()
        {
            var (_, _, aliceOrg, _) = await TwoOrganizationsWithAProductAsync();
            ActAs(userId: 1, organizationId: null);

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Products.Add(SneakyProduct(aliceOrg));

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => db.SaveChangesAsync());
        }

        [Fact]
        public async Task AnonymousWork_SuchAsSeeding_IsNotBlockedByTheWriteGuard()
        {
            ActAnonymously();

            using (var scope = App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var defaultOrganization = await db.Organizations.SingleAsync(o => o.Code == "default");

                db.Products.Add(SneakyProduct(defaultOrganization.Id));
                await db.SaveChangesAsync();
            }

            Assert.Equal(1, await TotalProductsAsync());
        }
    }
}
