using EnterpriseOMS.Application.Common.Exceptions;
using EnterpriseOMS.Application.IntegrationTests.Support;
using EnterpriseOMS.Application.Modules.Auth.DTOs;
using EnterpriseOMS.Application.Modules.Warehouses.Commands.SetStorageLocationActive;
using EnterpriseOMS.Application.Modules.Warehouses.Commands.SetWarehouseActive;
using EnterpriseOMS.Application.Modules.Warehouses.Commands.UpdateStorageLocation;
using EnterpriseOMS.Application.Modules.Warehouses.DTOs;
using EnterpriseOMS.Domain.Common.Exceptions;
using EnterpriseOMS.Domain.Entities.Warehousing;
using EnterpriseOMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.IntegrationTests.Handlers
{
    public class StorageLocationCommandHandlerTests : IntegrationTestBase
    {
        // ---------- helpers ----------

        private async Task<AuthResultDto> SignInAsync(string email = "alice@acme.test", string organization = "Acme Traders")
        {
            var registered = await RegisterAsync(email, organization);
            await ActAsAsync(registered);
            return registered;
        }

        private async Task<StorageLocationDto> RenameAsync(int warehouseId, StorageLocationDto current, string? name)
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<UpdateStorageLocationCommandHandler>();

            return await handler.Handle(
                new UpdateStorageLocationCommand { WarehouseId = warehouseId, Id = current.Id, Version = current.Version, Name = name },
                CancellationToken.None);
        }

        private async Task<StorageLocationDto> SetActiveAsync(int warehouseId, int id, Guid version, bool isActive)
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<SetStorageLocationActiveCommandHandler>();

            return await handler.Handle(
                new SetStorageLocationActiveCommand { WarehouseId = warehouseId, Id = id, Version = version, IsActive = isActive },
                CancellationToken.None);
        }

        private async Task DeactivateWarehouseAsync(WarehouseDto warehouse)
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<SetWarehouseActiveCommandHandler>();

            await handler.Handle(
                new SetWarehouseActiveCommand { Id = warehouse.Id, Version = warehouse.Version, IsActive = false },
                CancellationToken.None);
        }

        /// <summary>Reads the stored row, ignoring tenant filters, so tests can check what really is in the database.</summary>
        private async Task<StorageLocation> LoadAsync(int id)
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            return await db.StorageLocations.AsNoTracking().IgnoreQueryFilters().SingleAsync(l => l.Id == id);
        }

        private async Task<int> CountLocationsAsync()
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            return await db.StorageLocations.IgnoreQueryFilters().CountAsync();
        }

        // ---------- create ----------

        [Fact]
        public async Task Create_ATopLevelLocation_BelongsToTheWarehouseAndItsOrganization()
        {
            var alice = await SignInAsync();
            var organizationId = await OrganizationIdOfAsync(alice.UserId);
            var warehouse = await CreateWarehouseAsync("WH-MAIN");

            var zone = await CreateLocationAsync(warehouse.Id, "A", StorageLocationType.Zone);

            var stored = await LoadAsync(zone.Id);
            Assert.Equal(warehouse.Id, stored.WarehouseId);
            Assert.Equal(organizationId, stored.OrganizationId);
            Assert.Null(stored.ParentId);
        }

        [Fact]
        public async Task Create_ReturnsTheFullDetails()
        {
            await SignInAsync();
            var warehouse = await CreateWarehouseAsync("WH-MAIN");

            var zone = await CreateLocationAsync(warehouse.Id, "  a ", StorageLocationType.Zone, name: "  Ambient  ");

            Assert.True(zone.Id > 0);
            Assert.Equal(warehouse.Id, zone.WarehouseId);
            Assert.Null(zone.ParentId);
            Assert.Equal("a", zone.Code);
            Assert.Equal("Ambient", zone.Name);
            Assert.Equal(StorageLocationType.Zone, zone.Type);
            Assert.True(zone.IsActive);
            Assert.NotEqual(Guid.Empty, zone.Version);
            Assert.Equal(App.Clock.UtcNow, zone.Created);
            Assert.Null(zone.LastModified);
        }

        [Fact]
        public async Task Create_AChild_LinksToItsParent()
        {
            await SignInAsync();
            var warehouse = await CreateWarehouseAsync("WH-MAIN");
            var zone = await CreateLocationAsync(warehouse.Id, "A", StorageLocationType.Zone);

            var aisle = await CreateLocationAsync(warehouse.Id, "A-01", StorageLocationType.Aisle, parentId: zone.Id);

            Assert.Equal(zone.Id, aisle.ParentId);
            Assert.Equal(zone.Id, (await LoadAsync(aisle.Id)).ParentId);
        }

        [Fact]
        public async Task Create_TheFullChain_ZoneAisleRackShelfBin_IsAccepted()
        {
            await SignInAsync();
            var warehouse = await CreateWarehouseAsync("WH-MAIN");

            var zone = await CreateLocationAsync(warehouse.Id, "A", StorageLocationType.Zone);
            var aisle = await CreateLocationAsync(warehouse.Id, "A1", StorageLocationType.Aisle, zone.Id);
            var rack = await CreateLocationAsync(warehouse.Id, "A1-R1", StorageLocationType.Rack, aisle.Id);
            var shelf = await CreateLocationAsync(warehouse.Id, "A1-R1-S1", StorageLocationType.Shelf, rack.Id);
            var bin = await CreateLocationAsync(warehouse.Id, "A1-R1-S1-B1", StorageLocationType.Bin, shelf.Id);

            Assert.Equal(shelf.Id, bin.ParentId);
            Assert.Equal(5, await CountLocationsAsync());
        }

        [Fact]
        public async Task Create_SkippingLevels_IsAccepted()
        {
            await SignInAsync();
            var warehouse = await CreateWarehouseAsync("WH-MAIN");
            var zone = await CreateLocationAsync(warehouse.Id, "A", StorageLocationType.Zone);

            var bin = await CreateLocationAsync(warehouse.Id, "QUICK", StorageLocationType.Bin, zone.Id);

            Assert.Equal(zone.Id, bin.ParentId);
        }

        [Fact]
        public async Task Create_ALocationAtTheSameLevelAsItsParent_IsRejected_AndSavesNothing()
        {
            await SignInAsync();
            var warehouse = await CreateWarehouseAsync("WH-MAIN");
            var zone = await CreateLocationAsync(warehouse.Id, "A", StorageLocationType.Zone);

            var exception = await Assert.ThrowsAsync<DomainException>(
                () => CreateLocationAsync(warehouse.Id, "B", StorageLocationType.Zone, zone.Id));

            Assert.Contains("cannot be placed inside", exception.Message);
            Assert.Equal(1, await CountLocationsAsync());
        }

        [Theory]
        [InlineData("A-01")]
        [InlineData("a-01")]
        [InlineData("  A-01  ")]
        public async Task Create_DuplicateCodeInTheSameWarehouse_IsRejected_AndSavesNothingMore(string duplicateCode)
        {
            await SignInAsync();
            var warehouse = await CreateWarehouseAsync("WH-MAIN");
            await CreateLocationAsync(warehouse.Id, "A-01", StorageLocationType.Aisle);

            var exception = await Assert.ThrowsAsync<ValidationException>(
                () => CreateLocationAsync(warehouse.Id, duplicateCode, StorageLocationType.Aisle));

            Assert.Equal("A location with this code already exists in this warehouse.", Assert.Single(exception.Errors["Code"]));
            Assert.Equal(1, await CountLocationsAsync());
        }

        [Fact]
        public async Task Create_TheSameCodeInAnotherWarehouse_IsAllowed()
        {
            await SignInAsync();
            var main = await CreateWarehouseAsync("WH-MAIN");
            var east = await CreateWarehouseAsync("WH-EAST");

            await CreateLocationAsync(main.Id, "A-01", StorageLocationType.Aisle);
            await CreateLocationAsync(east.Id, "A-01", StorageLocationType.Aisle);

            Assert.Equal(2, await CountLocationsAsync());
        }

        [Fact]
        public async Task Create_InAnUnknownWarehouse_ThrowsNotFound()
        {
            await SignInAsync();

            await Assert.ThrowsAsync<NotFoundException>(
                () => CreateLocationAsync(9999, "A", StorageLocationType.Zone));
        }

        [Fact]
        public async Task Create_InAWarehouseOfAnotherOrganization_IsNotFound()
        {
            await SignInAsync("alice@acme.test", "Acme Traders");
            var alicesWarehouse = await CreateWarehouseAsync("WH-MAIN");

            await SignInAsync("bob@globex.test", "Globex");

            await Assert.ThrowsAsync<NotFoundException>(
                () => CreateLocationAsync(alicesWarehouse.Id, "A", StorageLocationType.Zone));
            Assert.Equal(0, await CountLocationsAsync());
        }

        [Fact]
        public async Task Create_WithNoOrganizationInTheToken_IsNotFound_BecauseNothingIsVisible()
        {
            await SignInAsync();
            var warehouse = await CreateWarehouseAsync("WH-MAIN");
            ActAs(userId: 1, organizationId: null);

            await Assert.ThrowsAsync<NotFoundException>(
                () => CreateLocationAsync(warehouse.Id, "A", StorageLocationType.Zone));
        }

        [Fact]
        public async Task Create_WithAParentFromAnotherWarehouse_IsRejected_OnTheParentField()
        {
            await SignInAsync();
            var main = await CreateWarehouseAsync("WH-MAIN");
            var east = await CreateWarehouseAsync("WH-EAST");
            var zoneInEast = await CreateLocationAsync(east.Id, "A", StorageLocationType.Zone);

            var exception = await Assert.ThrowsAsync<ValidationException>(
                () => CreateLocationAsync(main.Id, "A-01", StorageLocationType.Aisle, zoneInEast.Id));

            Assert.Equal("The parent location was not found in this warehouse.", Assert.Single(exception.Errors["ParentId"]));
        }

        [Fact]
        public async Task Create_WithAnUnknownParent_IsRejected_OnTheParentField()
        {
            await SignInAsync();
            var warehouse = await CreateWarehouseAsync("WH-MAIN");

            var exception = await Assert.ThrowsAsync<ValidationException>(
                () => CreateLocationAsync(warehouse.Id, "A-01", StorageLocationType.Aisle, parentId: 9999));

            Assert.True(exception.Errors.ContainsKey("ParentId"));
        }

        [Fact]
        public async Task Create_WithAParentOfAnotherOrganization_IsRejected_AsIfItDidNotExist()
        {
            await SignInAsync("alice@acme.test", "Acme Traders");
            var alicesWarehouse = await CreateWarehouseAsync("WH-MAIN");
            var alicesZone = await CreateLocationAsync(alicesWarehouse.Id, "A", StorageLocationType.Zone);

            await SignInAsync("bob@globex.test", "Globex");
            var bobsWarehouse = await CreateWarehouseAsync("WH-MAIN");

            var exception = await Assert.ThrowsAsync<ValidationException>(
                () => CreateLocationAsync(bobsWarehouse.Id, "A-01", StorageLocationType.Aisle, alicesZone.Id));

            Assert.True(exception.Errors.ContainsKey("ParentId"));
        }

        [Fact]
        public async Task Create_UnderAnInactiveParent_IsRejected()
        {
            await SignInAsync();
            var warehouse = await CreateWarehouseAsync("WH-MAIN");
            var zone = await CreateLocationAsync(warehouse.Id, "A", StorageLocationType.Zone);
            await SetActiveAsync(warehouse.Id, zone.Id, zone.Version, isActive: false);

            var exception = await Assert.ThrowsAsync<DomainException>(
                () => CreateLocationAsync(warehouse.Id, "A-01", StorageLocationType.Aisle, zone.Id));

            Assert.Equal("The parent location is inactive. Reactivate it first.", exception.Message);
        }

        [Fact]
        public async Task Create_InAnInactiveWarehouse_IsRejected()
        {
            await SignInAsync();
            var warehouse = await CreateWarehouseAsync("WH-MAIN");
            await DeactivateWarehouseAsync(warehouse);

            var exception = await Assert.ThrowsAsync<DomainException>(
                () => CreateLocationAsync(warehouse.Id, "A", StorageLocationType.Zone));

            Assert.Equal("Locations cannot be added to an inactive warehouse.", exception.Message);
        }

        // ---------- rename ----------

        [Fact]
        public async Task Rename_ChangesOnlyTheName_AndIssuesANewVersion()
        {
            await SignInAsync();
            var warehouse = await CreateWarehouseAsync("WH-MAIN");
            var zone = await CreateLocationAsync(warehouse.Id, "A", StorageLocationType.Zone);
            var aisle = await CreateLocationAsync(warehouse.Id, "A-01", StorageLocationType.Aisle, zone.Id);

            var renamed = await RenameAsync(warehouse.Id, aisle, "  First aisle  ");

            Assert.Equal("First aisle", renamed.Name);
            Assert.NotEqual(aisle.Version, renamed.Version);

            var stored = await LoadAsync(aisle.Id);
            Assert.Equal("First aisle", stored.Name);
            Assert.Equal("A-01", stored.Code);
            Assert.Equal(StorageLocationType.Aisle, stored.Type);
            Assert.Equal(zone.Id, stored.ParentId);
        }

        [Fact]
        public async Task Rename_ToABlankName_ClearsIt()
        {
            await SignInAsync();
            var warehouse = await CreateWarehouseAsync("WH-MAIN");
            var zone = await CreateLocationAsync(warehouse.Id, "A", StorageLocationType.Zone, name: "Ambient");

            await RenameAsync(warehouse.Id, zone, "   ");

            Assert.Null((await LoadAsync(zone.Id)).Name);
        }

        [Fact]
        public async Task Rename_WithAStaleVersion_IsRejected_AndChangesNothing()
        {
            await SignInAsync();
            var warehouse = await CreateWarehouseAsync("WH-MAIN");
            var zone = await CreateLocationAsync(warehouse.Id, "A", StorageLocationType.Zone, name: "Original");
            var first = await RenameAsync(warehouse.Id, zone, "First edit");

            await Assert.ThrowsAsync<ConcurrencyException>(() => RenameAsync(warehouse.Id, zone, "Second edit"));

            var stored = await LoadAsync(zone.Id);
            Assert.Equal("First edit", stored.Name);
            Assert.Equal(first.Version, stored.Version);
        }

        [Fact]
        public async Task Rename_UnknownLocation_ThrowsNotFound()
        {
            await SignInAsync();
            var warehouse = await CreateWarehouseAsync("WH-MAIN");
            var ghost = new StorageLocationDto { Id = 9999, Version = Guid.NewGuid() };

            await Assert.ThrowsAsync<NotFoundException>(() => RenameAsync(warehouse.Id, ghost, "Ghost"));
        }

        [Fact]
        public async Task Rename_ThroughTheWrongWarehouse_IsNotFound_EvenIfTheLocationExists()
        {
            await SignInAsync();
            var main = await CreateWarehouseAsync("WH-MAIN");
            var east = await CreateWarehouseAsync("WH-EAST");
            var zoneInMain = await CreateLocationAsync(main.Id, "A", StorageLocationType.Zone, name: "Original");

            await Assert.ThrowsAsync<NotFoundException>(() => RenameAsync(east.Id, zoneInMain, "Hacked"));

            Assert.Equal("Original", (await LoadAsync(zoneInMain.Id)).Name);
        }

        [Fact]
        public async Task Rename_ALocationOfAnotherOrganization_IsNotFound_AndStaysUntouched()
        {
            await SignInAsync("alice@acme.test", "Acme Traders");
            var alicesWarehouse = await CreateWarehouseAsync("WH-MAIN");
            var alicesZone = await CreateLocationAsync(alicesWarehouse.Id, "A", StorageLocationType.Zone, name: "Original");

            await SignInAsync("bob@globex.test", "Globex");

            await Assert.ThrowsAsync<NotFoundException>(() => RenameAsync(alicesWarehouse.Id, alicesZone, "Hacked"));

            Assert.Equal("Original", (await LoadAsync(alicesZone.Id)).Name);
        }

        // ---------- deactivate / reactivate ----------

        [Fact]
        public async Task Deactivate_ALeaf_FlipsTheFlag_AndIssuesANewVersion()
        {
            await SignInAsync();
            var warehouse = await CreateWarehouseAsync("WH-MAIN");
            var zone = await CreateLocationAsync(warehouse.Id, "A", StorageLocationType.Zone);

            var deactivated = await SetActiveAsync(warehouse.Id, zone.Id, zone.Version, isActive: false);

            Assert.False(deactivated.IsActive);
            Assert.NotEqual(zone.Version, deactivated.Version);
            Assert.False((await LoadAsync(zone.Id)).IsActive);
        }

        [Fact]
        public async Task Deactivate_WhileItHasActiveChildren_IsRefused_AndStaysActive()
        {
            await SignInAsync();
            var warehouse = await CreateWarehouseAsync("WH-MAIN");
            var zone = await CreateLocationAsync(warehouse.Id, "A", StorageLocationType.Zone);
            await CreateLocationAsync(warehouse.Id, "A-01", StorageLocationType.Aisle, zone.Id);

            var exception = await Assert.ThrowsAsync<DomainException>(
                () => SetActiveAsync(warehouse.Id, zone.Id, zone.Version, isActive: false));

            Assert.Equal("Deactivate its 1 active sub-location(s) first.", exception.Message);
            Assert.True((await LoadAsync(zone.Id)).IsActive);
        }

        [Fact]
        public async Task Deactivate_WorksBottomUp_ChildrenFirstThenTheParent()
        {
            await SignInAsync();
            var warehouse = await CreateWarehouseAsync("WH-MAIN");
            var zone = await CreateLocationAsync(warehouse.Id, "A", StorageLocationType.Zone);
            var aisle = await CreateLocationAsync(warehouse.Id, "A-01", StorageLocationType.Aisle, zone.Id);

            await SetActiveAsync(warehouse.Id, aisle.Id, aisle.Version, isActive: false);
            await SetActiveAsync(warehouse.Id, zone.Id, zone.Version, isActive: false);

            Assert.False((await LoadAsync(aisle.Id)).IsActive);
            Assert.False((await LoadAsync(zone.Id)).IsActive);
        }

        [Fact]
        public async Task Reactivate_WhileTheParentIsInactive_IsRefused()
        {
            await SignInAsync();
            var warehouse = await CreateWarehouseAsync("WH-MAIN");
            var zone = await CreateLocationAsync(warehouse.Id, "A", StorageLocationType.Zone);
            var aisle = await CreateLocationAsync(warehouse.Id, "A-01", StorageLocationType.Aisle, zone.Id);
            var aisleOff = await SetActiveAsync(warehouse.Id, aisle.Id, aisle.Version, isActive: false);
            await SetActiveAsync(warehouse.Id, zone.Id, zone.Version, isActive: false);

            var exception = await Assert.ThrowsAsync<DomainException>(
                () => SetActiveAsync(warehouse.Id, aisle.Id, aisleOff.Version, isActive: true));

            Assert.Equal("The parent location is inactive. Reactivate it first.", exception.Message);
            Assert.False((await LoadAsync(aisle.Id)).IsActive);
        }

        [Fact]
        public async Task Reactivate_WorksParentFirst_ThenTheChild()
        {
            await SignInAsync();
            var warehouse = await CreateWarehouseAsync("WH-MAIN");
            var zone = await CreateLocationAsync(warehouse.Id, "A", StorageLocationType.Zone);
            var aisle = await CreateLocationAsync(warehouse.Id, "A-01", StorageLocationType.Aisle, zone.Id);
            var aisleOff = await SetActiveAsync(warehouse.Id, aisle.Id, aisle.Version, isActive: false);
            var zoneOff = await SetActiveAsync(warehouse.Id, zone.Id, zone.Version, isActive: false);

            await SetActiveAsync(warehouse.Id, zone.Id, zoneOff.Version, isActive: true);
            await SetActiveAsync(warehouse.Id, aisle.Id, aisleOff.Version, isActive: true);

            Assert.True((await LoadAsync(zone.Id)).IsActive);
            Assert.True((await LoadAsync(aisle.Id)).IsActive);
        }

        [Fact]
        public async Task Deactivate_WithAStaleVersion_IsRejected()
        {
            await SignInAsync();
            var warehouse = await CreateWarehouseAsync("WH-MAIN");
            var zone = await CreateLocationAsync(warehouse.Id, "A", StorageLocationType.Zone);
            await RenameAsync(warehouse.Id, zone, "Someone else renamed it first");

            await Assert.ThrowsAsync<ConcurrencyException>(
                () => SetActiveAsync(warehouse.Id, zone.Id, zone.Version, isActive: false));

            Assert.True((await LoadAsync(zone.Id)).IsActive);
        }

        [Fact]
        public async Task Deactivate_ThroughTheWrongWarehouse_IsNotFound()
        {
            await SignInAsync();
            var main = await CreateWarehouseAsync("WH-MAIN");
            var east = await CreateWarehouseAsync("WH-EAST");
            var zone = await CreateLocationAsync(main.Id, "A", StorageLocationType.Zone);

            await Assert.ThrowsAsync<NotFoundException>(
                () => SetActiveAsync(east.Id, zone.Id, zone.Version, isActive: false));

            Assert.True((await LoadAsync(zone.Id)).IsActive);
        }

        [Fact]
        public async Task Deactivate_ALocationOfAnotherOrganization_IsNotFound_AndStaysActive()
        {
            await SignInAsync("alice@acme.test", "Acme Traders");
            var alicesWarehouse = await CreateWarehouseAsync("WH-MAIN");
            var alicesZone = await CreateLocationAsync(alicesWarehouse.Id, "A", StorageLocationType.Zone);

            await SignInAsync("bob@globex.test", "Globex");

            await Assert.ThrowsAsync<NotFoundException>(
                () => SetActiveAsync(alicesWarehouse.Id, alicesZone.Id, alicesZone.Version, isActive: false));

            Assert.True((await LoadAsync(alicesZone.Id)).IsActive);
        }
    }
}
