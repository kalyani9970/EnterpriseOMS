using EnterpriseOMS.Application.Common.Exceptions;
using EnterpriseOMS.Application.Common.Models;
using EnterpriseOMS.Application.IntegrationTests.Support;
using EnterpriseOMS.Application.Modules.Auth.DTOs;
using EnterpriseOMS.Application.Modules.Warehouses.Commands.SetStorageLocationActive;
using EnterpriseOMS.Application.Modules.Warehouses.Commands.SetWarehouseActive;
using EnterpriseOMS.Application.Modules.Warehouses.DTOs;
using EnterpriseOMS.Application.Modules.Warehouses.Queries.GetWarehouseById;
using EnterpriseOMS.Application.Modules.Warehouses.Queries.GetWarehouseLocations;
using EnterpriseOMS.Application.Modules.Warehouses.Queries.GetWarehousesList;
using EnterpriseOMS.Domain.Entities.Warehousing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.IntegrationTests.Handlers
{
    public class WarehouseQueryHandlerTests : IntegrationTestBase
    {
        // Created one minute apart, in this order. WH-OLD is deactivated after creation.
        private static readonly (string Code, string Name, string City)[] Catalog =
        {
        ("WH-MAIN", "Main Distribution Center", "Rotterdam"),
        ("WH-EAST", "East Fulfilment Hub", "Antwerp"),
        ("WH-NORTH", "North Cross-dock", "Hamburg"),
        ("WH-OLD", "Closed Depot", "Ghent"),
        ("WH-OUT", "50% Outlet", "Utrecht")
    };

        private readonly Dictionary<string, WarehouseDto> _created = new();

        // ---------- helpers ----------

        /// <summary>Five warehouses, plus three locations in WH-MAIN and one in WH-EAST.</summary>
        private async Task<AuthResultDto> SignInWithCatalogAsync()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            await ActAsAsync(alice);

            foreach (var (code, name, city) in Catalog)
            {
                App.Clock.Advance(TimeSpan.FromMinutes(1));
                _created[code] = await CreateWarehouseAsync(code, name, city);
            }

            await DeactivateWarehouseAsync(_created["WH-OLD"]);

            var zoneA = await CreateLocationAsync(_created["WH-MAIN"].Id, "A", StorageLocationType.Zone);
            await CreateLocationAsync(_created["WH-MAIN"].Id, "A-01", StorageLocationType.Aisle, zoneA.Id);
            await CreateLocationAsync(_created["WH-MAIN"].Id, "B", StorageLocationType.Zone);
            await CreateLocationAsync(_created["WH-EAST"].Id, "Z1", StorageLocationType.Zone);

            return alice;
        }

        private async Task DeactivateWarehouseAsync(WarehouseDto warehouse)
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<SetWarehouseActiveCommandHandler>();

            await handler.Handle(
                new SetWarehouseActiveCommand { Id = warehouse.Id, Version = warehouse.Version, IsActive = false },
                CancellationToken.None);
        }

        private async Task DeactivateLocationAsync(int warehouseId, StorageLocationDto location)
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<SetStorageLocationActiveCommandHandler>();

            await handler.Handle(
                new SetStorageLocationActiveCommand
                {
                    WarehouseId = warehouseId,
                    Id = location.Id,
                    Version = location.Version,
                    IsActive = false
                },
                CancellationToken.None);
        }

        private async Task<PaginatedList<WarehouseListItemDto>> ListAsync(Action<GetWarehousesListQuery>? configure = null)
        {
            var query = new GetWarehousesListQuery();
            configure?.Invoke(query);

            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<GetWarehousesListQueryHandler>();

            return await handler.Handle(query, CancellationToken.None);
        }

        private async Task<WarehouseDto> GetByIdAsync(int id)
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<GetWarehouseByIdQueryHandler>();

            return await handler.Handle(new GetWarehouseByIdQuery { Id = id }, CancellationToken.None);
        }

        private async Task<List<StorageLocationDto>> LocationsAsync(int warehouseId, bool activeOnly = false)
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<GetWarehouseLocationsQueryHandler>();

            return await handler.Handle(
                new GetWarehouseLocationsQuery { WarehouseId = warehouseId, ActiveOnly = activeOnly },
                CancellationToken.None);
        }

        private static List<string> NamesOf(PaginatedList<WarehouseListItemDto> page) =>
            page.Items.Select(item => item.Name).ToList();

        private static List<string> CodesOf(PaginatedList<WarehouseListItemDto> page) =>
            page.Items.Select(item => item.Code).ToList();

        // ---------- get by id ----------

        [Fact]
        public async Task GetById_ReturnsTheFullDetail_WithTheAddressAndLocationCount()
        {
            await SignInWithCatalogAsync();

            var warehouse = await GetByIdAsync(_created["WH-MAIN"].Id);

            Assert.Equal("WH-MAIN", warehouse.Code);
            Assert.Equal("Main Distribution Center", warehouse.Name);
            Assert.Equal("1 Test Street", warehouse.Address.Line1);
            Assert.Equal("Rotterdam", warehouse.Address.City);
            Assert.Equal("NL", warehouse.Address.CountryCode);
            Assert.Equal(3, warehouse.LocationCount);
            Assert.True(warehouse.IsActive);
            Assert.Equal(_created["WH-MAIN"].Version, warehouse.Version);
        }

        [Fact]
        public async Task GetById_AnInactiveWarehouse_IsStillReadable()
        {
            await SignInWithCatalogAsync();

            var warehouse = await GetByIdAsync(_created["WH-OLD"].Id);

            Assert.False(warehouse.IsActive);
        }

        [Fact]
        public async Task GetById_UnknownId_ThrowsNotFound()
        {
            await SignInWithCatalogAsync();

            await Assert.ThrowsAsync<NotFoundException>(() => GetByIdAsync(9999));
        }

        [Fact]
        public async Task GetById_AWarehouseOfAnotherOrganization_ThrowsNotFound()
        {
            await SignInWithCatalogAsync();
            var alicesWarehouse = _created["WH-MAIN"];

            var bob = await RegisterAsync("bob@globex.test", "Globex");
            await ActAsAsync(bob);

            await Assert.ThrowsAsync<NotFoundException>(() => GetByIdAsync(alicesWarehouse.Id));
        }

        // ---------- list: defaults and paging ----------

        [Fact]
        public async Task List_Default_ReturnsEveryWarehouse_SortedByName()
        {
            await SignInWithCatalogAsync();

            var page = await ListAsync();

            Assert.Equal(
                new[] { "50% Outlet", "Closed Depot", "East Fulfilment Hub", "Main Distribution Center", "North Cross-dock" },
                NamesOf(page));
            Assert.Equal(5, page.TotalCount);
            Assert.Equal(1, page.TotalPages);
            Assert.False(page.HasPreviousPage);
            Assert.False(page.HasNextPage);
        }

        [Fact]
        public async Task List_Paging_SplitsTheResultIntoPages()
        {
            await SignInWithCatalogAsync();

            var first = await ListAsync(q => { q.PageSize = 2; q.PageNumber = 1; });
            var second = await ListAsync(q => { q.PageSize = 2; q.PageNumber = 2; });
            var third = await ListAsync(q => { q.PageSize = 2; q.PageNumber = 3; });

            Assert.Equal(new[] { "50% Outlet", "Closed Depot" }, NamesOf(first));
            Assert.Equal(new[] { "East Fulfilment Hub", "Main Distribution Center" }, NamesOf(second));
            Assert.Equal(new[] { "North Cross-dock" }, NamesOf(third));

            Assert.Equal(5, first.TotalCount);
            Assert.Equal(3, first.TotalPages);
            Assert.False(first.HasPreviousPage);
            Assert.True(first.HasNextPage);
            Assert.True(third.HasPreviousPage);
            Assert.False(third.HasNextPage);
        }

        [Fact]
        public async Task List_APageBeyondTheEnd_HasNoItems_ButTheRealTotals()
        {
            await SignInWithCatalogAsync();

            var page = await ListAsync(q => { q.PageSize = 2; q.PageNumber = 10; });

            Assert.Empty(page.Items);
            Assert.Equal(5, page.TotalCount);
            Assert.Equal(3, page.TotalPages);
        }

        // ---------- list: search ----------

        [Theory]
        [InlineData("hub")]
        [InlineData("HUB")]
        public async Task List_Search_MatchesTheName_IgnoringCase(string search)
        {
            await SignInWithCatalogAsync();

            var page = await ListAsync(q => q.Search = search);

            Assert.Equal(new[] { "WH-EAST" }, CodesOf(page));
        }

        [Fact]
        public async Task List_Search_MatchesPartOfTheCode()
        {
            await SignInWithCatalogAsync();

            var page = await ListAsync(q => q.Search = "WH-O");

            Assert.Equal(new[] { "WH-OUT", "WH-OLD" }, CodesOf(page));   // by name: "50% Outlet", "Closed Depot"
        }

        [Fact]
        public async Task List_Search_MatchesTheCity()
        {
            await SignInWithCatalogAsync();

            var page = await ListAsync(q => q.Search = "rotter");

            Assert.Equal(new[] { "WH-MAIN" }, CodesOf(page));
        }

        [Fact]
        public async Task List_Search_TreatsAPercentSignAsALiteralCharacter()
        {
            await SignInWithCatalogAsync();

            // If "%" acted as a wildcard this would match everything. It must match only "50% Outlet".
            var page = await ListAsync(q => q.Search = "%");

            Assert.Equal(new[] { "50% Outlet" }, NamesOf(page));
        }

        [Fact]
        public async Task List_Search_TreatsAnUnderscoreAsALiteralCharacter()
        {
            await SignInWithCatalogAsync();

            var page = await ListAsync(q => q.Search = "_");

            Assert.Empty(page.Items);
        }

        [Theory]
        [InlineData("[a]")]
        [InlineData("\\")]
        [InlineData("'; DROP TABLE Warehouses; --")]
        public async Task List_Search_SpecialCharacters_DoNotBreakTheQuery(string search)
        {
            await SignInWithCatalogAsync();

            var page = await ListAsync(q => q.Search = search);

            Assert.Empty(page.Items);

            // ...and the table is still there.
            Assert.Equal(5, (await ListAsync()).TotalCount);
        }

        // ---------- list: filter and sort ----------

        [Theory]
        [InlineData(true, 4)]
        [InlineData(false, 1)]
        [InlineData(null, 5)]
        public async Task List_StatusFilter_SeparatesActiveFromInactive(bool? isActive, int expectedCount)
        {
            await SignInWithCatalogAsync();

            var page = await ListAsync(q => q.IsActive = isActive);

            Assert.Equal(expectedCount, page.TotalCount);
        }

        [Theory]
        [InlineData("code", false, "WH-EAST", "WH-OUT")]
        [InlineData("code", true, "WH-OUT", "WH-EAST")]
        [InlineData("name", false, "WH-OUT", "WH-NORTH")]     // "50% Outlet" ... "North Cross-dock"
        [InlineData("name", true, "WH-NORTH", "WH-OUT")]
        [InlineData("city", false, "WH-EAST", "WH-OUT")]      // Antwerp ... Utrecht
        [InlineData("city", true, "WH-OUT", "WH-EAST")]
        [InlineData("created", false, "WH-MAIN", "WH-OUT")]
        [InlineData("created", true, "WH-OUT", "WH-MAIN")]
        public async Task List_Sorting_OrdersByTheChosenField(string sortBy, bool descending, string firstCode, string lastCode)
        {
            await SignInWithCatalogAsync();

            var codes = CodesOf(await ListAsync(q => { q.SortBy = sortBy; q.Descending = descending; }));

            Assert.Equal(5, codes.Count);
            Assert.Equal(firstCode, codes[0]);
            Assert.Equal(lastCode, codes[^1]);
        }

        [Fact]
        public async Task List_EqualSortValues_KeepAStableOrder()
        {
            var bob = await RegisterAsync("bob@globex.test", "Globex");
            await ActAsAsync(bob);
            await CreateWarehouseAsync("T-2", name: "Same Name");
            await CreateWarehouseAsync("T-1", name: "Same Name");

            var ascending = CodesOf(await ListAsync(q => q.SortBy = "name"));
            var descending = CodesOf(await ListAsync(q => { q.SortBy = "name"; q.Descending = true; }));

            // Ties are broken by creation order (Id), so paging never repeats or skips a row.
            Assert.Equal(new[] { "T-2", "T-1" }, ascending);
            Assert.Equal(new[] { "T-2", "T-1" }, descending);
        }

        [Fact]
        public async Task List_FilterSortAndPaging_WorkTogether()
        {
            await SignInWithCatalogAsync();

            var first = await ListAsync(q =>
            {
                q.IsActive = true;
                q.SortBy = "city";
                q.Descending = true;
                q.PageSize = 2;
                q.PageNumber = 1;
            });
            var second = await ListAsync(q =>
            {
                q.IsActive = true;
                q.SortBy = "city";
                q.Descending = true;
                q.PageSize = 2;
                q.PageNumber = 2;
            });

            Assert.Equal(new[] { "WH-OUT", "WH-MAIN" }, CodesOf(first));      // Utrecht, Rotterdam
            Assert.Equal(new[] { "WH-NORTH", "WH-EAST" }, CodesOf(second));   // Hamburg, Antwerp
            Assert.Equal(4, first.TotalCount);
            Assert.Equal(2, first.TotalPages);
        }

        // ---------- list: shape and isolation ----------

        [Fact]
        public async Task List_OnlyShowsTheCallersOrganization()
        {
            var alice = await SignInWithCatalogAsync();

            var bob = await RegisterAsync("bob@globex.test", "Globex");
            await ActAsAsync(bob);
            await CreateWarehouseAsync("BOB-1", name: "Bob's Warehouse");

            Assert.Equal(new[] { "BOB-1" }, CodesOf(await ListAsync()));

            await ActAsAsync(alice);
            var asAlice = await ListAsync();
            Assert.Equal(5, asAlice.TotalCount);
            Assert.DoesNotContain("BOB-1", string.Join(",", CodesOf(asAlice)));
        }

        [Fact]
        public async Task List_ItemsCarryTheFieldsATableRowNeeds()
        {
            await SignInWithCatalogAsync();
            var created = _created["WH-MAIN"];

            var item = (await ListAsync()).Items.Single(i => i.Code == "WH-MAIN");

            Assert.Equal(created.Id, item.Id);
            Assert.Equal("Main Distribution Center", item.Name);
            Assert.Equal("Rotterdam", item.City);
            Assert.Equal("NL", item.CountryCode);
            Assert.True(item.IsActive);
            Assert.Equal(created.Version, item.Version);
        }

        [Fact]
        public async Task List_EachRowCountsItsOwnLocations()
        {
            await SignInWithCatalogAsync();

            var counts = (await ListAsync()).Items.ToDictionary(i => i.Code, i => i.LocationCount);

            Assert.Equal(3, counts["WH-MAIN"]);
            Assert.Equal(1, counts["WH-EAST"]);
            Assert.Equal(0, counts["WH-NORTH"]);
            Assert.Equal(0, counts["WH-OLD"]);
            Assert.Equal(0, counts["WH-OUT"]);
        }

        // ---------- locations ----------

        [Fact]
        public async Task Locations_AreReturnedInTreeOrder_NotCreationOrder()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            await ActAsAsync(alice);
            var warehouse = await CreateWarehouseAsync("WH-MAIN");

            // Deliberately created in a scrambled order.
            var zoneB = await CreateLocationAsync(warehouse.Id, "B", StorageLocationType.Zone);
            var zoneA = await CreateLocationAsync(warehouse.Id, "A", StorageLocationType.Zone);
            await CreateLocationAsync(warehouse.Id, "A-02", StorageLocationType.Aisle, zoneA.Id);
            var aisle1 = await CreateLocationAsync(warehouse.Id, "A-01", StorageLocationType.Aisle, zoneA.Id);
            await CreateLocationAsync(warehouse.Id, "A-01-R1", StorageLocationType.Rack, aisle1.Id);
            await CreateLocationAsync(warehouse.Id, "B-01", StorageLocationType.Aisle, zoneB.Id);

            var codes = (await LocationsAsync(warehouse.Id)).Select(l => l.Code).ToList();

            // Each parent is directly followed by its descendants; siblings are in code order.
            Assert.Equal(new[] { "A", "A-01", "A-01-R1", "A-02", "B", "B-01" }, codes);
        }

        [Fact]
        public async Task Locations_ByDefault_IncludeInactiveOnes()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            await ActAsAsync(alice);
            var warehouse = await CreateWarehouseAsync("WH-MAIN");
            var zone = await CreateLocationAsync(warehouse.Id, "A", StorageLocationType.Zone);
            var aisle = await CreateLocationAsync(warehouse.Id, "A-01", StorageLocationType.Aisle, zone.Id);
            await DeactivateLocationAsync(warehouse.Id, aisle);

            var locations = await LocationsAsync(warehouse.Id);

            Assert.Equal(2, locations.Count);
            Assert.False(locations.Single(l => l.Code == "A-01").IsActive);
        }

        [Fact]
        public async Task Locations_ActiveOnly_LeavesOutInactiveBranches()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            await ActAsAsync(alice);
            var warehouse = await CreateWarehouseAsync("WH-MAIN");
            var zoneA = await CreateLocationAsync(warehouse.Id, "A", StorageLocationType.Zone);
            var zoneB = await CreateLocationAsync(warehouse.Id, "B", StorageLocationType.Zone);
            var aisleB = await CreateLocationAsync(warehouse.Id, "B-01", StorageLocationType.Aisle, zoneB.Id);
            await CreateLocationAsync(warehouse.Id, "A-01", StorageLocationType.Aisle, zoneA.Id);

            // Retire the whole B branch, child first.
            await DeactivateLocationAsync(warehouse.Id, aisleB);
            await DeactivateLocationAsync(warehouse.Id, zoneB);

            var codes = (await LocationsAsync(warehouse.Id, activeOnly: true)).Select(l => l.Code).ToList();

            Assert.Equal(new[] { "A", "A-01" }, codes);
        }

        [Fact]
        public async Task Locations_OfAnUnknownWarehouse_ThrowsNotFound()
        {
            await SignInWithCatalogAsync();

            await Assert.ThrowsAsync<NotFoundException>(() => LocationsAsync(9999));
        }

        [Fact]
        public async Task Locations_OfAWarehouseOfAnotherOrganization_ThrowsNotFound()
        {
            await SignInWithCatalogAsync();
            var alicesWarehouse = _created["WH-MAIN"];

            var bob = await RegisterAsync("bob@globex.test", "Globex");
            await ActAsAsync(bob);

            await Assert.ThrowsAsync<NotFoundException>(() => LocationsAsync(alicesWarehouse.Id));
        }

        [Fact]
        public async Task Locations_OfAWarehouseWithNone_IsAnEmptyList()
        {
            await SignInWithCatalogAsync();

            Assert.Empty(await LocationsAsync(_created["WH-NORTH"].Id));
        }

        [Fact]
        public async Task Locations_OnlyCoverTheRequestedWarehouse()
        {
            await SignInWithCatalogAsync();

            var main = await LocationsAsync(_created["WH-MAIN"].Id);
            var east = await LocationsAsync(_created["WH-EAST"].Id);

            Assert.Equal(new[] { "A", "A-01", "B" }, main.Select(l => l.Code));
            Assert.Equal(new[] { "Z1" }, east.Select(l => l.Code));
            Assert.All(main, location => Assert.Equal(_created["WH-MAIN"].Id, location.WarehouseId));
        }

        [Fact]
        public async Task Locations_CarryTheFieldsATreeNeeds()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            await ActAsAsync(alice);
            var warehouse = await CreateWarehouseAsync("WH-MAIN");
            var zone = await CreateLocationAsync(warehouse.Id, "A", StorageLocationType.Zone, name: "Ambient");
            var aisle = await CreateLocationAsync(warehouse.Id, "A-01", StorageLocationType.Aisle, zone.Id);

            var locations = await LocationsAsync(warehouse.Id);

            var root = locations[0];
            Assert.Equal(zone.Id, root.Id);
            Assert.Null(root.ParentId);
            Assert.Equal("Ambient", root.Name);
            Assert.Equal(StorageLocationType.Zone, root.Type);
            Assert.Equal(zone.Version, root.Version);

            var child = locations[1];
            Assert.Equal(aisle.Id, child.Id);
            Assert.Equal(zone.Id, child.ParentId);
            Assert.Null(child.Name);
            Assert.Equal(StorageLocationType.Aisle, child.Type);
            Assert.True(child.IsActive);
        }
    }
}
