using EnterpriseOMS.Application.IntegrationTests.Support;
using EnterpriseOMS.Domain.Entities.Catalog;
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
    public class ProductPersistenceTests : IntegrationTestBase
    {
        private static Product NewProduct(int organizationId, string sku = "LAP-001", decimal unitPrice = 899.00m, decimal costPrice = 690.00m) =>
        Product.Create(organizationId, sku, "Laptop", null, null, UnitOfMeasure.Box, unitPrice, costPrice, 5);

        private async Task<int> DefaultOrganizationIdAsync()
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            return (await db.Organizations.IgnoreQueryFilters().SingleAsync(o => o.Code == "default")).Id;
        }

        // ---------- optimistic concurrency, at the database level ----------

        [Fact]
        public async Task TwoPeopleEditingTheSameProduct_TheSecondSaveIsRejectedByTheDatabase()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            await ActAsAsync(alice);
            var created = await CreateProductAsync("LAP-001");

            // Two separate DbContexts = two people who both opened the product before either saved.
            using var scopeA = App.Services.CreateScope();
            using var scopeB = App.Services.CreateScope();
            var dbA = scopeA.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbB = scopeB.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var productA = await dbA.Products.SingleAsync(p => p.Id == created.Id);
            var productB = await dbB.Products.SingleAsync(p => p.Id == created.Id);

            productA.Update("Edited by A", null, null, UnitOfMeasure.Piece, 10m, 5m, 1);
            await dbA.SaveChangesAsync();

            productB.Update("Edited by B", null, null, UnitOfMeasure.Piece, 20m, 5m, 1);
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());

            // A's work survived; B's was refused rather than silently overwriting it.
            using var verifyScope = App.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stored = await verifyDb.Products.AsNoTracking().SingleAsync(p => p.Id == created.Id);
            Assert.Equal("Edited by A", stored.Name);
        }

        [Fact]
        public async Task AnUpdateRacingADeactivate_IsRejected()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            await ActAsAsync(alice);
            var created = await CreateProductAsync("LAP-001");

            using var scopeA = App.Services.CreateScope();
            using var scopeB = App.Services.CreateScope();
            var dbA = scopeA.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbB = scopeB.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var productA = await dbA.Products.SingleAsync(p => p.Id == created.Id);
            var productB = await dbB.Products.SingleAsync(p => p.Id == created.Id);

            productA.Deactivate();
            await dbA.SaveChangesAsync();

            productB.Update("Edit based on the old copy", null, null, UnitOfMeasure.Piece, 10m, 5m, 1);
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());
        }

        [Fact]
        public async Task SeveralSavesInTheSameContext_DoNotConflictWithThemselves()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            await ActAsAsync(alice);
            var created = await CreateProductAsync("LAP-001");

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var product = await db.Products.SingleAsync(p => p.Id == created.Id);

            product.Update("First", null, null, UnitOfMeasure.Piece, 1m, 1m, 1);
            await db.SaveChangesAsync();
            product.Update("Second", null, null, UnitOfMeasure.Piece, 2m, 1m, 1);
            await db.SaveChangesAsync();   // must not throw: EF refreshed its idea of the stored Version after the first save

            Assert.Equal("Second", (await db.Products.AsNoTracking().SingleAsync(p => p.Id == created.Id)).Name);
        }

        [Fact]
        public async Task TheVersion_SurvivesARoundTripUnchanged()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            await ActAsAsync(alice);
            var created = await CreateProductAsync("LAP-001");

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var stored = await db.Products.AsNoTracking().SingleAsync(p => p.Id == created.Id);

            Assert.Equal(created.Version, stored.Version);
        }

        // ---------- uniqueness and relationships ----------

        [Fact]
        public async Task TheSameNormalizedSku_InOneOrganization_ViolatesTheUniqueIndex()
        {
            var organizationId = await DefaultOrganizationIdAsync();

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Products.Add(NewProduct(organizationId, "LAP-001"));
            db.Products.Add(NewProduct(organizationId, "lap-001"));   // normalizes to the same value

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        [Fact]
        public async Task TheSameSku_InTwoOrganizations_IsAllowed()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            var bob = await RegisterAsync("bob@globex.test", "Globex");
            await ActAsAsync(alice);
            await CreateProductAsync("LAP-001");
            await ActAsAsync(bob);
            await CreateProductAsync("LAP-001");

            ActAnonymously();
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            Assert.Equal(2, await db.Products.CountAsync(p => p.NormalizedSku == "LAP-001"));
        }

        [Fact]
        public async Task AProductForAnOrganizationThatDoesNotExist_IsRejectedByTheForeignKey()
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Products.Add(NewProduct(organizationId: 9999));

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        [Fact]
        public async Task AnOrganizationThatStillHasProducts_CannotBeDeleted()
        {
            int organizationId;
            using (var setup = App.Services.CreateScope())
            {
                var db = setup.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var organization = Organization.Create("Has Products", "has-products");
                db.Organizations.Add(organization);
                await db.SaveChangesAsync();

                organizationId = organization.Id;
                db.Products.Add(NewProduct(organizationId));
                await db.SaveChangesAsync();
            }

            using var scope = App.Services.CreateScope();
            var verifyDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var toDelete = await verifyDb.Organizations.SingleAsync(o => o.Id == organizationId);

            verifyDb.Organizations.Remove(toDelete);

            await Assert.ThrowsAsync<DbUpdateException>(() => verifyDb.SaveChangesAsync());
        }

        // ---------- how values are stored ----------

        [Fact]
        public async Task TheUnitOfMeasure_IsStoredAsReadableText()
        {
            var organizationId = await DefaultOrganizationIdAsync();
            int productId;

            using (var scope = App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var product = NewProduct(organizationId);   // created with UnitOfMeasure.Box
                db.Products.Add(product);
                await db.SaveChangesAsync();
                productId = product.Id;
            }

            using var readScope = App.Services.CreateScope();
            var readDb = readScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Bypass EF and read the raw column, to see exactly what is in the database.
            using var command = readDb.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT UnitOfMeasure FROM Products WHERE Id = $id";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "$id";
            parameter.Value = productId;
            command.Parameters.Add(parameter);

            Assert.Equal("Box", (string?)await command.ExecuteScalarAsync());
        }

        [Fact]
        public async Task Prices_RoundTripWithTwoDecimals()
        {
            var organizationId = await DefaultOrganizationIdAsync();
            int productId;

            using (var scope = App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var product = NewProduct(organizationId, unitPrice: 899.99m, costPrice: 690.50m);
                db.Products.Add(product);
                await db.SaveChangesAsync();
                productId = product.Id;
            }

            using var readScope = App.Services.CreateScope();
            var readDb = readScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stored = await readDb.Products.AsNoTracking().SingleAsync(p => p.Id == productId);

            Assert.Equal(899.99m, stored.UnitPrice);
            Assert.Equal(690.50m, stored.CostPrice);
        }

        [Fact]
        public async Task OptionalTextFields_PersistAsNull()
        {
            var organizationId = await DefaultOrganizationIdAsync();
            int productId;

            using (var scope = App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var product = NewProduct(organizationId);   // no description, no category
                db.Products.Add(product);
                await db.SaveChangesAsync();
                productId = product.Id;
            }

            using var readScope = App.Services.CreateScope();
            var readDb = readScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stored = await readDb.Products.AsNoTracking().SingleAsync(p => p.Id == productId);

            Assert.Null(stored.Description);
            Assert.Null(stored.Category);
        }
    }
}
