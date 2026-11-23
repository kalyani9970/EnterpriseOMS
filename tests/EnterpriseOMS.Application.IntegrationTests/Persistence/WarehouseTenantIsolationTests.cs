using EnterpriseOMS.Application.Common.Exceptions;
using EnterpriseOMS.Application.IntegrationTests.Support;
using EnterpriseOMS.Application.Modules.Warehouses.DTOs;
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
    /// <summary>
    /// Warehouses and their locations are tenant data like products: whatever a handler forgets, the
    /// database layer must keep one organization's rows away from another's, in both directions:
    /// reading (query filter) and writing (the SaveChanges guard).
    /// </summary>
    public class WarehouseTenantIsolationTests : IntegrationTestBase
    {
        private sealed record TwoTenants(
        WarehouseDto AliceWarehouse,
        StorageLocationDto AliceZone,
        WarehouseDto BobWarehouse,
        StorageLocationDto BobZone,
        int AliceOrg,
        int BobOrg);

        /// <summary>Each organization gets one warehouse with one zone. Bob is left signed in.</summary>
        private async Task<TwoTenants> TwoOrganizationsAsync()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            var bob = await RegisterAsync("bob@globex.test", "Globex");

            await ActAsAsync(alice);
            var aliceWarehouse = await CreateWarehouseAsync("ALICE-WH", "Alice's Warehouse");
            var aliceZone = await CreateLocationAsync(aliceWarehouse.Id, "A", StorageLocationType.Zone);

            await ActAsAsync(bob);
            var bobWarehouse = await CreateWarehouseAsync("BOB-WH", "Bob's Warehouse");
            var bobZone = await CreateLocationAsync(bobWarehouse.Id, "B", StorageLocationType.Zone);

            return new TwoTenants(
                aliceWarehouse,
                aliceZone,
                bobWarehouse,
                bobZone,
                await OrganizationIdOfAsync(alice.UserId),
                await OrganizationIdOfAsync(bob.UserId));
        }

        private static Address SomeAddress() => Address.Create("1 Test Street", null, "Utrecht", null, null, "NL");

        private async Task<int> TotalWarehousesAsync()
        {
            ActAnonymously();
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            return await db.Warehouses.CountAsync();
        }

        private async Task<int> TotalLocationsAsync()
        {
            ActAnonymously();
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            return await db.StorageLocations.CountAsync();
        }

        [Fact]
        public async Task Bob_CannotSeeAlicesWarehouses()
        {
            var tenants = await TwoOrganizationsAsync();   // currently signed in as Bob

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var visible = await db.Warehouses.Select(w => w.Code).ToListAsync();

            Assert.Equal(new[] { "BOB-WH" }, visible);
            Assert.Null(await db.Warehouses.FirstOrDefaultAsync(w => w.Id == tenants.AliceWarehouse.Id));
        }

        [Fact]
        public async Task Bob_CannotSeeAlicesStorageLocations()
        {
            var tenants = await TwoOrganizationsAsync();   // currently signed in as Bob

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var visible = await db.StorageLocations.Select(l => l.Code).ToListAsync();

            Assert.Equal(new[] { "B" }, visible);
            Assert.Null(await db.StorageLocations.FirstOrDefaultAsync(l => l.Id == tenants.AliceZone.Id));
        }

        [Fact]
        public async Task Anonymous_SeesWarehousesAndLocationsOfEveryOrganization()
        {
            await TwoOrganizationsAsync();

            Assert.Equal(2, await TotalWarehousesAsync());
            Assert.Equal(2, await TotalLocationsAsync());
        }

        [Fact]
        public async Task IgnoreQueryFilters_BypassesBothFilters()
        {
            await TwoOrganizationsAsync();   // signed in as Bob

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            Assert.Equal(1, await db.Warehouses.CountAsync());
            Assert.Equal(2, await db.Warehouses.IgnoreQueryFilters().CountAsync());
            Assert.Equal(1, await db.StorageLocations.CountAsync());
            Assert.Equal(2, await db.StorageLocations.IgnoreQueryFilters().CountAsync());
        }

        [Fact]
        public async Task TheFilterFollowsTheCurrentUser_EvenOnTheSameDbContext()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            var bob = await RegisterAsync("bob@globex.test", "Globex");
            await ActAsAsync(alice);
            await CreateWarehouseAsync("ALICE-WH");
            await ActAsAsync(bob);
            await CreateWarehouseAsync("BOB-WH");

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await ActAsAsync(alice);
            var asAlice = await db.Warehouses.Select(w => w.Code).ToListAsync();

            await ActAsAsync(bob);
            var asBob = await db.Warehouses.Select(w => w.Code).ToListAsync();

            Assert.Equal(new[] { "ALICE-WH" }, asAlice);
            Assert.Equal(new[] { "BOB-WH" }, asBob);
        }

        [Fact]
        public async Task ASignedInUser_CannotSaveAWarehouseForAnotherOrganization()
        {
            var tenants = await TwoOrganizationsAsync();   // signed in as Bob

            using (var scope = App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Warehouses.Add(Warehouse.Create(tenants.AliceOrg, "SNEAKY", "Sneaky", SomeAddress()));   // Bob plants one in Alice's organization

                await Assert.ThrowsAsync<ForbiddenAccessException>(() => db.SaveChangesAsync());
            }

            Assert.Equal(2, await TotalWarehousesAsync());   // nothing was saved
        }

        [Fact]
        public async Task ASignedInUser_CannotSaveAStorageLocationForAnotherOrganization()
        {
            var tenants = await TwoOrganizationsAsync();   // signed in as Bob

            using (var scope = App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Even if a buggy handler fetched Alice's warehouse (here by bypassing the filter), a location built
                // from it belongs to Alice's organization, and the write guard refuses to save it for Bob.
                var alicesWarehouse = await db.Warehouses.IgnoreQueryFilters()
                    .SingleAsync(w => w.Id == tenants.AliceWarehouse.Id);
                db.StorageLocations.Add(StorageLocation.Create(alicesWarehouse, null, "SNEAKY", null, StorageLocationType.Zone));

                await Assert.ThrowsAsync<ForbiddenAccessException>(() => db.SaveChangesAsync());
            }

            Assert.Equal(2, await TotalLocationsAsync());   // nothing was saved
        }

        [Fact]
        public async Task ASignedInUser_CannotMoveAWarehouseToAnotherOrganization()
        {
            var tenants = await TwoOrganizationsAsync();   // signed in as Bob

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var warehouse = await db.Warehouses.SingleAsync(w => w.Id == tenants.BobWarehouse.Id);

            db.Entry(warehouse).Property(nameof(Warehouse.OrganizationId)).CurrentValue = tenants.AliceOrg;

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => db.SaveChangesAsync());
        }

        [Fact]
        public async Task ASignedInUser_CannotMoveAStorageLocationToAnotherOrganization()
        {
            var tenants = await TwoOrganizationsAsync();   // signed in as Bob

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var location = await db.StorageLocations.SingleAsync(l => l.Id == tenants.BobZone.Id);

            db.Entry(location).Property(nameof(StorageLocation.OrganizationId)).CurrentValue = tenants.AliceOrg;

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => db.SaveChangesAsync());
        }

        [Fact]
        public async Task ASignedInUserWithoutAnOrganization_CannotSaveAnyWarehouse()
        {
            var tenants = await TwoOrganizationsAsync();
            ActAs(userId: 1, organizationId: null);

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Warehouses.Add(Warehouse.Create(tenants.AliceOrg, "SNEAKY", "Sneaky", SomeAddress()));

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => db.SaveChangesAsync());
        }

        [Fact]
        public async Task AnonymousWork_SuchAsSeeding_IsNotBlockedByTheWriteGuard()
        {
            ActAnonymously();

            using (var scope = App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var defaultOrganization = await db.Organizations.SingleAsync(o => o.Code == IdentitySeeder.DefaultOrganizationCode);

                db.Warehouses.Add(Warehouse.Create(defaultOrganization.Id, "SEEDED", "Seeded", SomeAddress()));
                await db.SaveChangesAsync();
            }

            Assert.Equal(1, await TotalWarehousesAsync());
        }
    }
}
