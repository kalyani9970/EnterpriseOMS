using EnterpriseOMS.Application.IntegrationTests.Support;
using EnterpriseOMS.Domain.Entities.Warehousing;
using EnterpriseOMS.Infrastructure.Persistence;
using EnterpriseOMS.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.IntegrationTests.Persistence
{
    public class WarehouseDemoSeederTests : IntegrationTestBase
    {
        private async Task SeedAsync()
        {
            ActAnonymously();
            using var scope = App.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<WarehouseDemoSeeder>().SeedAsync();
        }

        private async Task<List<Warehouse>> AllWarehousesAsync()
        {
            ActAnonymously();
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            return await db.Warehouses.AsNoTracking().OrderBy(w => w.Id).ToListAsync();
        }

        private async Task<List<StorageLocation>> AllLocationsAsync()
        {
            ActAnonymously();
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            return await db.StorageLocations.AsNoTracking().OrderBy(l => l.Id).ToListAsync();
        }

        private async Task<int> DefaultOrganizationIdAsync()
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            return (await db.Organizations.IgnoreQueryFilters()
                .SingleAsync(o => o.Code == IdentitySeeder.DefaultOrganizationCode)).Id;
        }

        [Fact]
        public async Task Seed_CreatesThreeWarehouses_InTheDefaultOrganization()
        {
            await SeedAsync();

            var warehouses = await AllWarehousesAsync();
            var defaultOrganizationId = await DefaultOrganizationIdAsync();

            Assert.Equal(3, warehouses.Count);
            Assert.All(warehouses, warehouse => Assert.Equal(defaultOrganizationId, warehouse.OrganizationId));
        }

        [Fact]
        public async Task Seed_IncludesExactlyOneInactiveWarehouse_SoTheStatusFilterHasSomethingToShow()
        {
            await SeedAsync();

            var inactive = (await AllWarehousesAsync()).Where(w => !w.IsActive).ToList();

            Assert.Equal("WH-OLD", Assert.Single(inactive).Code);
        }

        [Fact]
        public async Task Seed_UsesDistinctCodes()
        {
            await SeedAsync();

            var warehouses = await AllWarehousesAsync();

            Assert.Equal(warehouses.Count, warehouses.Select(w => w.NormalizedCode).Distinct().Count());
        }

        [Fact]
        public async Task Seed_GivesTheWarehousesRealisticAddresses()
        {
            await SeedAsync();

            var warehouses = (await AllWarehousesAsync()).ToDictionary(w => w.Code);

            Assert.Equal("Rotterdam", warehouses["WH-MAIN"].Address.City);
            Assert.Equal("NL", warehouses["WH-MAIN"].Address.CountryCode);
            Assert.Equal("Antwerp", warehouses["WH-EAST"].Address.City);
            Assert.Equal("BE", warehouses["WH-EAST"].Address.CountryCode);
            Assert.Equal("Dock 3", warehouses["WH-EAST"].Address.Line2);
        }

        [Fact]
        public async Task Seed_CreatesElevenLocations_NineInMainTwoInEast_NoneInTheClosedDepot()
        {
            await SeedAsync();

            var warehouses = (await AllWarehousesAsync()).ToDictionary(w => w.Code, w => w.Id);
            var locations = await AllLocationsAsync();

            Assert.Equal(11, locations.Count);
            Assert.Equal(9, locations.Count(l => l.WarehouseId == warehouses["WH-MAIN"]));
            Assert.Equal(2, locations.Count(l => l.WarehouseId == warehouses["WH-EAST"]));
            Assert.Equal(0, locations.Count(l => l.WarehouseId == warehouses["WH-OLD"]));
        }

        [Fact]
        public async Task Seed_BuildsTheFullDepthTree_ZoneAisleRackShelfBin()
        {
            await SeedAsync();

            var locations = await AllLocationsAsync();
            var byId = locations.ToDictionary(l => l.Id);
            var bin = locations.Single(l => l.Code == "A-01-R1-S1-B1");

            var chain = new List<string>();
            var current = byId[bin.ParentId!.Value];
            while (true)
            {
                chain.Add(current.Code);
                if (current.ParentId is null) break;
                current = byId[current.ParentId.Value];
            }

            Assert.Equal(StorageLocationType.Bin, bin.Type);
            Assert.Equal(new[] { "A-01-R1-S1", "A-01-R1", "A-01", "A" }, chain);
        }

        [Fact]
        public async Task Seed_EveryLocationObeysTheLevelRule()
        {
            await SeedAsync();

            var locations = await AllLocationsAsync();
            var byId = locations.ToDictionary(l => l.Id);

            Assert.All(
                locations.Where(l => l.ParentId is not null),
                location => Assert.True(location.Type > byId[location.ParentId!.Value].Type));
        }

        [Fact]
        public async Task Seed_EveryLocationLivesInTheSameWarehouseAsItsParent()
        {
            await SeedAsync();

            var locations = await AllLocationsAsync();
            var byId = locations.ToDictionary(l => l.Id);

            Assert.All(
                locations.Where(l => l.ParentId is not null),
                location => Assert.Equal(byId[location.ParentId!.Value].WarehouseId, location.WarehouseId));
        }

        [Fact]
        public async Task Seed_RunTwice_AddsNothing()
        {
            await SeedAsync();
            await SeedAsync();

            Assert.Equal(3, (await AllWarehousesAsync()).Count);
            Assert.Equal(11, (await AllLocationsAsync()).Count);
        }

        [Fact]
        public async Task Seed_WhenDisabledInConfiguration_AddsNothing()
        {
            using (var scope = App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var seeder = new WarehouseDemoSeeder(
                    db,
                    Options.Create(new SeedDemoDataSettings { Warehouses = false }),
                    NullLogger<WarehouseDemoSeeder>.Instance);

                await seeder.SeedAsync();
            }

            Assert.Empty(await AllWarehousesAsync());
            Assert.Empty(await AllLocationsAsync());
        }

        [Fact]
        public async Task Seed_LeavesAnOrganizationThatAlreadyHasWarehousesAlone()
        {
            var defaultOrganizationId = await DefaultOrganizationIdAsync();

            using (var scope = App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Warehouses.Add(Warehouse.Create(
                    defaultOrganizationId, "MINE-1", "My own warehouse",
                    EnterpriseOMS.Domain.ValueObjects.Address.Create("1 Test Street", null, "Utrecht", null, null, "NL")));
                await db.SaveChangesAsync();
            }

            await SeedAsync();

            var warehouses = await AllWarehousesAsync();
            Assert.Equal("MINE-1", Assert.Single(warehouses).Code);
            Assert.Empty(await AllLocationsAsync());
        }

        [Fact]
        public async Task Seed_DoesNotTouchOtherOrganizations()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            await ActAsAsync(alice);
            await CreateWarehouseAsync("ALICE-1");

            await SeedAsync();

            var warehouses = await AllWarehousesAsync();
            var alicesOrganizationId = await OrganizationIdOfAsync(alice.UserId);

            Assert.Equal(4, warehouses.Count);   // 3 demo warehouses + Alice's own
            Assert.Equal(
                new[] { "ALICE-1" },
                warehouses.Where(w => w.OrganizationId == alicesOrganizationId).Select(w => w.Code));
        }
    }
}
