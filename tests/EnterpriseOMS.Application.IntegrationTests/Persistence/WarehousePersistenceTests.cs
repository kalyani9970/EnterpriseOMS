using EnterpriseOMS.Application.IntegrationTests.Support;
using EnterpriseOMS.Domain.Entities.Tenancy;
using EnterpriseOMS.Domain.Entities.Warehousing;
using EnterpriseOMS.Domain.ValueObjects;
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
    public class WarehousePersistenceTests : IntegrationTestBase
    {
        private static Address SomeAddress(string city = "Utrecht") =>
        Address.Create("1 Test Street", null, city, null, null, "NL");

        private async Task<int> DefaultOrganizationIdAsync()
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            return (await db.Organizations.IgnoreQueryFilters()
                .SingleAsync(o => o.Code == IdentitySeeder.DefaultOrganizationCode)).Id;
        }

        /// <summary>Saves a warehouse with a zone and an aisle inside it, anonymously (like a seeder), and returns their ids.</summary>
        private async Task<(int WarehouseId, int ZoneId, int AisleId)> SeedTreeAsync(int organizationId, string code = "WH-TREE")
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var warehouse = Warehouse.Create(organizationId, code, "Tree", SomeAddress());
            db.Warehouses.Add(warehouse);
            await db.SaveChangesAsync();

            var zone = StorageLocation.Create(warehouse, null, "A", null, StorageLocationType.Zone);
            db.StorageLocations.Add(zone);
            await db.SaveChangesAsync();

            var aisle = StorageLocation.Create(warehouse, zone, "A-01", null, StorageLocationType.Aisle);
            db.StorageLocations.Add(aisle);
            await db.SaveChangesAsync();

            return (warehouse.Id, zone.Id, aisle.Id);
        }

        // ---------- optimistic concurrency, at the database level ----------

        [Fact]
        public async Task TwoPeopleEditingTheSameWarehouse_TheSecondSaveIsRejectedByTheDatabase()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            await ActAsAsync(alice);
            var created = await CreateWarehouseAsync("WH-MAIN");

            // Two separate DbContexts = two people who both opened the warehouse before either saved.
            using var scopeA = App.Services.CreateScope();
            using var scopeB = App.Services.CreateScope();
            var dbA = scopeA.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbB = scopeB.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var warehouseA = await dbA.Warehouses.SingleAsync(w => w.Id == created.Id);
            var warehouseB = await dbB.Warehouses.SingleAsync(w => w.Id == created.Id);

            warehouseA.Update("Edited by A", SomeAddress("Antwerp"));
            await dbA.SaveChangesAsync();

            warehouseB.Update("Edited by B", SomeAddress("Ghent"));
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());

            // A's work survived, including the address; B's was refused instead of silently overwriting it.
            using var verifyScope = App.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stored = await verifyDb.Warehouses.AsNoTracking().SingleAsync(w => w.Id == created.Id);
            Assert.Equal("Edited by A", stored.Name);
            Assert.Equal("Antwerp", stored.Address.City);
        }

        [Fact]
        public async Task AnUpdateRacingADeactivate_IsRejected()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            await ActAsAsync(alice);
            var created = await CreateWarehouseAsync("WH-MAIN");

            using var scopeA = App.Services.CreateScope();
            using var scopeB = App.Services.CreateScope();
            var dbA = scopeA.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbB = scopeB.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var warehouseA = await dbA.Warehouses.SingleAsync(w => w.Id == created.Id);
            var warehouseB = await dbB.Warehouses.SingleAsync(w => w.Id == created.Id);

            warehouseA.Deactivate();
            await dbA.SaveChangesAsync();

            warehouseB.Update("Edit based on the old copy", SomeAddress());
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());
        }

        [Fact]
        public async Task SeveralSavesInTheSameContext_DoNotConflictWithThemselves()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            await ActAsAsync(alice);
            var created = await CreateWarehouseAsync("WH-MAIN");

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var warehouse = await db.Warehouses.SingleAsync(w => w.Id == created.Id);

            warehouse.Update("First", SomeAddress("Antwerp"));
            await db.SaveChangesAsync();
            warehouse.Update("Second", SomeAddress("Ghent"));
            await db.SaveChangesAsync();   // must not throw: EF refreshed its idea of the stored Version after the first save

            var stored = await db.Warehouses.AsNoTracking().SingleAsync(w => w.Id == created.Id);
            Assert.Equal("Second", stored.Name);
            Assert.Equal("Ghent", stored.Address.City);
        }

        [Fact]
        public async Task TheWarehouseVersion_SurvivesARoundTripUnchanged()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            await ActAsAsync(alice);
            var created = await CreateWarehouseAsync("WH-MAIN");

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var stored = await db.Warehouses.AsNoTracking().SingleAsync(w => w.Id == created.Id);

            Assert.Equal(created.Version, stored.Version);
        }

        [Fact]
        public async Task TwoPeopleRenamingTheSameLocation_TheSecondSaveIsRejectedByTheDatabase()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            await ActAsAsync(alice);
            var warehouse = await CreateWarehouseAsync("WH-MAIN");
            var zone = await CreateLocationAsync(warehouse.Id, "A", StorageLocationType.Zone);

            using var scopeA = App.Services.CreateScope();
            using var scopeB = App.Services.CreateScope();
            var dbA = scopeA.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbB = scopeB.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var locationA = await dbA.StorageLocations.SingleAsync(l => l.Id == zone.Id);
            var locationB = await dbB.StorageLocations.SingleAsync(l => l.Id == zone.Id);

            locationA.Rename("By A");
            await dbA.SaveChangesAsync();

            locationB.Rename("By B");
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());

            using var verifyScope = App.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal("By A", (await verifyDb.StorageLocations.AsNoTracking().SingleAsync(l => l.Id == zone.Id)).Name);
        }

        // ---------- uniqueness ----------

        [Fact]
        public async Task TheSameNormalizedWarehouseCode_InOneOrganization_ViolatesTheUniqueIndex()
        {
            var organizationId = await DefaultOrganizationIdAsync();

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Warehouses.Add(Warehouse.Create(organizationId, "WH-MAIN", "One", SomeAddress()));
            db.Warehouses.Add(Warehouse.Create(organizationId, "wh-main", "Two", SomeAddress()));   // normalizes to the same value

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        [Fact]
        public async Task TheSameWarehouseCode_InTwoOrganizations_IsAllowed()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            var bob = await RegisterAsync("bob@globex.test", "Globex");
            await ActAsAsync(alice);
            await CreateWarehouseAsync("WH-MAIN");
            await ActAsAsync(bob);
            await CreateWarehouseAsync("WH-MAIN");

            ActAnonymously();
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            Assert.Equal(2, await db.Warehouses.CountAsync(w => w.NormalizedCode == "WH-MAIN"));
        }

        [Fact]
        public async Task TheSameLocationCode_InOneWarehouse_ViolatesTheUniqueIndex()
        {
            var organizationId = await DefaultOrganizationIdAsync();
            var tree = await SeedTreeAsync(organizationId);   // already contains a location with code "A"

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var warehouse = await db.Warehouses.SingleAsync(w => w.Id == tree.WarehouseId);
            db.StorageLocations.Add(StorageLocation.Create(warehouse, null, "a", null, StorageLocationType.Zone));

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        [Fact]
        public async Task TheSameLocationCode_InTwoWarehouses_IsAllowed()
        {
            var organizationId = await DefaultOrganizationIdAsync();
            await SeedTreeAsync(organizationId, "WH-1");
            await SeedTreeAsync(organizationId, "WH-2");

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            Assert.Equal(2, await db.StorageLocations.CountAsync(l => l.NormalizedCode == "A"));
        }

        [Fact]
        public async Task TwoWarehouses_MayShareAName_BecauseOnlyTheCodeIsUnique()
        {
            var organizationId = await DefaultOrganizationIdAsync();

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Warehouses.Add(Warehouse.Create(organizationId, "WH-1", "Same Name", SomeAddress()));
            db.Warehouses.Add(Warehouse.Create(organizationId, "WH-2", "Same Name", SomeAddress()));

            await db.SaveChangesAsync();

            Assert.Equal(2, await db.Warehouses.CountAsync(w => w.Name == "Same Name"));
        }

        // ---------- relationships ----------

        [Fact]
        public async Task AWarehouseForAnOrganizationThatDoesNotExist_IsRejectedByTheForeignKey()
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Warehouses.Add(Warehouse.Create(9999, "WH-GHOST", "Ghost", SomeAddress()));

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        [Fact]
        public async Task AnOrganizationThatStillHasWarehouses_CannotBeDeleted()
        {
            int organizationId;
            using (var setup = App.Services.CreateScope())
            {
                var db = setup.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var organization = Organization.Create("Has Warehouses", "has-warehouses");
                db.Organizations.Add(organization);
                await db.SaveChangesAsync();

                organizationId = organization.Id;
                db.Warehouses.Add(Warehouse.Create(organizationId, "WH-1", "One", SomeAddress()));
                await db.SaveChangesAsync();
            }

            using var scope = App.Services.CreateScope();
            var verifyDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var toDelete = await verifyDb.Organizations.SingleAsync(o => o.Id == organizationId);

            verifyDb.Organizations.Remove(toDelete);

            await Assert.ThrowsAsync<DbUpdateException>(() => verifyDb.SaveChangesAsync());
        }

        [Fact]
        public async Task AWarehouseThatStillHasLocations_CannotBeDeleted()
        {
            var tree = await SeedTreeAsync(await DefaultOrganizationIdAsync());

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var warehouse = await db.Warehouses.SingleAsync(w => w.Id == tree.WarehouseId);

            db.Warehouses.Remove(warehouse);

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        [Fact]
        public async Task ALocationThatStillHasChildren_CannotBeDeleted()
        {
            var tree = await SeedTreeAsync(await DefaultOrganizationIdAsync());

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var zone = await db.StorageLocations.SingleAsync(l => l.Id == tree.ZoneId);

            db.StorageLocations.Remove(zone);   // the aisle inside it still points at it

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        [Fact]
        public async Task ALocationWhoseParentDoesNotExist_IsRejectedByTheForeignKey()
        {
            var organizationId = await DefaultOrganizationIdAsync();
            var tree = await SeedTreeAsync(organizationId);

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var warehouse = await db.Warehouses.SingleAsync(w => w.Id == tree.WarehouseId);

            // A parent that was never saved: the domain accepts it (it only checks the rules), the database does not.
            var phantomParent = StorageLocation.Create(warehouse, null, "GHOST", null, StorageLocationType.Zone);
            phantomParent.Id = 9999;
            var child = StorageLocation.Create(warehouse, phantomParent, "KID", null, StorageLocationType.Aisle);

            db.StorageLocations.Add(child);

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        [Fact]
        public async Task DeactivatingAWarehouse_KeepsItsLocations()
        {
            var tree = await SeedTreeAsync(await DefaultOrganizationIdAsync());

            using (var scope = App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var warehouse = await db.Warehouses.SingleAsync(w => w.Id == tree.WarehouseId);
                warehouse.Deactivate();
                await db.SaveChangesAsync();
            }

            using var verifyScope = App.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Warehouses are never deleted, and deactivating one does not touch the locations inside it.
            Assert.Equal(2, await verifyDb.StorageLocations.CountAsync(l => l.WarehouseId == tree.WarehouseId));
            Assert.False((await verifyDb.Warehouses.SingleAsync(w => w.Id == tree.WarehouseId)).IsActive);
        }

        // ---------- how values are stored ----------

        [Fact]
        public async Task TheAddress_IsStoredAsPlainColumnsOnTheWarehouseRow()
        {
            var organizationId = await DefaultOrganizationIdAsync();
            int warehouseId;

            using (var scope = App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var warehouse = Warehouse.Create(
                    organizationId, "WH-ADDR", "Addressed",
                    Address.Create("100 Commerce Way", "Gate 4", "Rotterdam", "South Holland", "3011 AA", "nl"));
                db.Warehouses.Add(warehouse);
                await db.SaveChangesAsync();
                warehouseId = warehouse.Id;
            }

            using var readScope = App.Services.CreateScope();
            var readDb = readScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Bypass EF and read the raw columns, to see exactly what is in the database: no separate address table.
            using var command = readDb.Database.GetDbConnection().CreateCommand();
            command.CommandText =
                "SELECT AddressLine1, AddressLine2, City, Region, PostalCode, CountryCode FROM Warehouses WHERE Id = $id";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "$id";
            parameter.Value = warehouseId;
            command.Parameters.Add(parameter);

            using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("100 Commerce Way", reader.GetString(0));
            Assert.Equal("Gate 4", reader.GetString(1));
            Assert.Equal("Rotterdam", reader.GetString(2));
            Assert.Equal("South Holland", reader.GetString(3));
            Assert.Equal("3011 AA", reader.GetString(4));
            Assert.Equal("NL", reader.GetString(5));   // normalized to upper case before it was stored
        }

        [Fact]
        public async Task OptionalAddressParts_PersistAsNull()
        {
            var organizationId = await DefaultOrganizationIdAsync();
            int warehouseId;

            using (var scope = App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var warehouse = Warehouse.Create(organizationId, "WH-MIN", "Minimal", SomeAddress());   // no line 2, region or postal code
                db.Warehouses.Add(warehouse);
                await db.SaveChangesAsync();
                warehouseId = warehouse.Id;
            }

            using var readScope = App.Services.CreateScope();
            var readDb = readScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stored = await readDb.Warehouses.AsNoTracking().SingleAsync(w => w.Id == warehouseId);

            Assert.Null(stored.Address.Line2);
            Assert.Null(stored.Address.Region);
            Assert.Null(stored.Address.PostalCode);
            Assert.Equal("Utrecht", stored.Address.City);
        }

        [Fact]
        public async Task TheLocationType_IsStoredAsReadableText()
        {
            var tree = await SeedTreeAsync(await DefaultOrganizationIdAsync());

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT Type FROM StorageLocations WHERE Id = $id";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "$id";
            parameter.Value = tree.AisleId;
            command.Parameters.Add(parameter);

            Assert.Equal("Aisle", (string?)await command.ExecuteScalarAsync());
        }
    }
}
