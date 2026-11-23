using EnterpriseOMS.Domain.Common;
using EnterpriseOMS.Domain.Common.Exceptions;
using EnterpriseOMS.Domain.Entities.Warehousing;
using EnterpriseOMS.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.UnitTests.Entities
{
    public class StorageLocationTests
    {
        private static Warehouse MakeWarehouse(int id = 10, int organizationId = 7, string code = "WH")
        {
            var warehouse = Warehouse.Create(
                organizationId, code, "Main", Address.Create("12 Industrial Road", null, "Pune", null, null, "IN"));
            warehouse.Id = id;   // EF assigns the id when a real warehouse is saved; tests set it by hand
            return warehouse;
        }

        private static StorageLocation Make(
            Warehouse warehouse,
            StorageLocation? parent,
            string code,
            StorageLocationType type,
            string? name = null,
            int id = 0)
        {
            var location = StorageLocation.Create(warehouse, parent, code, name, type);
            location.Id = id;
            return location;
        }

        // ---------- creating a location ----------

        [Fact]
        public void Create_ATopLevelLocation_StoresTheDetails_AndBelongsToTheWarehouse()
        {
            var warehouse = MakeWarehouse();

            var zone = Make(warehouse, null, "  a ", StorageLocationType.Zone, "  Zone A ");

            Assert.Equal(7, zone.OrganizationId);
            Assert.Equal(10, zone.WarehouseId);
            Assert.Null(zone.ParentId);
            Assert.Equal("a", zone.Code);
            Assert.Equal("A", zone.NormalizedCode);
            Assert.Equal("Zone A", zone.Name);
            Assert.Equal(StorageLocationType.Zone, zone.Type);
            Assert.True(zone.IsActive);
            Assert.NotEqual(Guid.Empty, zone.Version);
        }

        [Fact]
        public void Create_AChild_LinksToItsParent_AndLeavesTheNameEmptyIfNoneIsGiven()
        {
            var warehouse = MakeWarehouse();
            var zone = Make(warehouse, null, "A", StorageLocationType.Zone, id: 1);

            var aisle = Make(warehouse, zone, "A1", StorageLocationType.Aisle);

            Assert.Equal(1, aisle.ParentId);
            Assert.Null(aisle.Name);
        }

        [Fact]
        public void Create_TheFullChain_ZoneAisleRackShelfBin_IsAllowed()
        {
            var warehouse = MakeWarehouse();
            var zone = Make(warehouse, null, "A", StorageLocationType.Zone, id: 1);
            var aisle = Make(warehouse, zone, "A1", StorageLocationType.Aisle, id: 2);
            var rack = Make(warehouse, aisle, "A1-R1", StorageLocationType.Rack, id: 3);
            var shelf = Make(warehouse, rack, "A1-R1-S1", StorageLocationType.Shelf, id: 4);

            var bin = Make(warehouse, shelf, "A1-R1-S1-B1", StorageLocationType.Bin);

            Assert.Equal(4, bin.ParentId);
        }

        [Theory]
        [InlineData(StorageLocationType.Zone, StorageLocationType.Aisle)]
        [InlineData(StorageLocationType.Zone, StorageLocationType.Bin)]      // levels may be skipped
        [InlineData(StorageLocationType.Aisle, StorageLocationType.Rack)]
        [InlineData(StorageLocationType.Rack, StorageLocationType.Shelf)]
        [InlineData(StorageLocationType.Shelf, StorageLocationType.Bin)]
        public void Create_AChildBelowItsParentsLevel_IsAllowed(StorageLocationType parentType, StorageLocationType childType)
        {
            var warehouse = MakeWarehouse();
            var parent = Make(warehouse, null, "P", parentType, id: 1);

            var child = Make(warehouse, parent, "C", childType);

            Assert.Equal(1, child.ParentId);
        }

        [Theory]
        [InlineData(StorageLocationType.Zone, StorageLocationType.Zone)]     // same level
        [InlineData(StorageLocationType.Aisle, StorageLocationType.Aisle)]
        [InlineData(StorageLocationType.Aisle, StorageLocationType.Zone)]    // a zone cannot be inside an aisle
        [InlineData(StorageLocationType.Rack, StorageLocationType.Aisle)]
        [InlineData(StorageLocationType.Shelf, StorageLocationType.Rack)]
        [InlineData(StorageLocationType.Bin, StorageLocationType.Bin)]       // nothing goes inside a bin
        [InlineData(StorageLocationType.Bin, StorageLocationType.Zone)]
        public void Create_AChildAtOrAboveItsParentsLevel_Throws(StorageLocationType parentType, StorageLocationType childType)
        {
            var warehouse = MakeWarehouse();
            var parent = Make(warehouse, null, "P", parentType, id: 1);

            var exception = Assert.Throws<DomainException>(() => Make(warehouse, parent, "C", childType));

            Assert.Contains("cannot be placed inside", exception.Message);
        }

        [Fact]
        public void Create_ABinMayBeTopLevel_BecauseAnyTypeMayBeARoot()
        {
            var bin = Make(MakeWarehouse(), null, "LOOSE", StorageLocationType.Bin);

            Assert.Null(bin.ParentId);
        }

        [Fact]
        public void Create_TheErrorNamesBothLevels()
        {
            var warehouse = MakeWarehouse();
            var aisle = Make(warehouse, null, "A1", StorageLocationType.Aisle, id: 1);

            var exception = Assert.Throws<DomainException>(() => Make(warehouse, aisle, "Z", StorageLocationType.Zone));

            Assert.Contains("A Zone cannot be placed inside a Aisle", exception.Message);
        }

        [Fact]
        public void Create_AParentFromAnotherWarehouse_Throws()
        {
            var main = MakeWarehouse(id: 10, code: "WH");
            var other = MakeWarehouse(id: 11, code: "WH2");
            var zoneInOther = Make(other, null, "A", StorageLocationType.Zone, id: 1);

            var exception = Assert.Throws<DomainException>(() => Make(main, zoneInOther, "A1", StorageLocationType.Aisle));

            Assert.Equal("The parent location belongs to a different warehouse.", exception.Message);
        }

        [Fact]
        public void Create_UnderAnInactiveParent_Throws()
        {
            var warehouse = MakeWarehouse();
            var zone = Make(warehouse, null, "A", StorageLocationType.Zone, id: 1);
            zone.Deactivate(activeChildCount: 0);

            var exception = Assert.Throws<DomainException>(() => Make(warehouse, zone, "A1", StorageLocationType.Aisle));

            Assert.Equal("The parent location is inactive. Reactivate it first.", exception.Message);
        }

        [Fact]
        public void Create_InAnInactiveWarehouse_Throws()
        {
            var warehouse = MakeWarehouse();
            warehouse.Deactivate();

            var exception = Assert.Throws<DomainException>(() => Make(warehouse, null, "A", StorageLocationType.Zone));

            Assert.Equal("Locations cannot be added to an inactive warehouse.", exception.Message);
        }

        // ---------- code, name and type rules ----------

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_EmptyCode_Throws(string code)
        {
            var exception = Assert.Throws<DomainException>(() => Make(MakeWarehouse(), null, code, StorageLocationType.Zone));

            Assert.Equal("Location code is required.", exception.Message);
        }

        [Fact]
        public void Create_CodeLongerThan30_Throws()
        {
            Assert.Throws<DomainException>(() => Make(MakeWarehouse(), null, new string('A', 31), StorageLocationType.Zone));
        }

        [Fact]
        public void Create_CodeOfExactly30Characters_Succeeds()
        {
            var location = Make(MakeWarehouse(), null, new string('A', 30), StorageLocationType.Zone);

            Assert.Equal(30, location.Code.Length);
        }

        [Theory]
        [InlineData("A B")]
        [InlineData("-A")]
        [InlineData("A#1")]
        public void Create_CodeWithIllegalCharacters_Throws(string code)
        {
            var exception = Assert.Throws<DomainException>(() => Make(MakeWarehouse(), null, code, StorageLocationType.Zone));

            Assert.Contains("may contain only", exception.Message);
        }

        [Fact]
        public void Create_UndefinedType_Throws()
        {
            var exception = Assert.Throws<DomainException>(() => Make(MakeWarehouse(), null, "A", (StorageLocationType)99));

            Assert.Equal("Location type is not valid.", exception.Message);
        }

        [Fact]
        public void Create_NameLongerThan200_Throws()
        {
            Assert.Throws<DomainException>(() => Make(MakeWarehouse(), null, "A", StorageLocationType.Zone, new string('x', 201)));
        }

        [Fact]
        public void Create_NameOfExactly200Characters_Succeeds()
        {
            var location = Make(MakeWarehouse(), null, "A", StorageLocationType.Zone, new string('x', 200));

            Assert.Equal(200, location.Name!.Length);
        }

        // ---------- renaming ----------

        [Fact]
        public void Rename_TrimsTheName_AndGivesANewVersion()
        {
            var location = Make(MakeWarehouse(), null, "A", StorageLocationType.Zone);
            var before = location.Version;

            location.Rename("  New name ");

            Assert.Equal("New name", location.Name);
            Assert.NotEqual(before, location.Version);
        }

        [Fact]
        public void Rename_ToABlankName_ClearsIt()
        {
            var location = Make(MakeWarehouse(), null, "A", StorageLocationType.Zone, "Old name");

            location.Rename("   ");

            Assert.Null(location.Name);
        }

        [Fact]
        public void Rename_NeverChangesTheCodeTypeOrParent()
        {
            var warehouse = MakeWarehouse();
            var zone = Make(warehouse, null, "A", StorageLocationType.Zone, id: 1);
            var aisle = Make(warehouse, zone, "A1", StorageLocationType.Aisle);

            aisle.Rename("Aisle one");

            Assert.Equal("A1", aisle.Code);
            Assert.Equal(StorageLocationType.Aisle, aisle.Type);
            Assert.Equal(1, aisle.ParentId);
        }

        [Fact]
        public void Rename_NameLongerThan200_Throws_AndKeepsTheOldName()
        {
            var location = Make(MakeWarehouse(), null, "A", StorageLocationType.Zone, "Old name");

            Assert.Throws<DomainException>(() => location.Rename(new string('x', 201)));

            Assert.Equal("Old name", location.Name);
        }

        // ---------- deactivating ----------

        [Fact]
        public void Deactivate_WithNoActiveChildren_FlipsTheFlag_AndChangesTheVersion()
        {
            var location = Make(MakeWarehouse(), null, "A", StorageLocationType.Zone);
            var before = location.Version;

            location.Deactivate(activeChildCount: 0);

            Assert.False(location.IsActive);
            Assert.NotEqual(before, location.Version);
        }

        [Fact]
        public void Deactivate_WhileItHasActiveChildren_Throws_AndStaysActive()
        {
            var location = Make(MakeWarehouse(), null, "A", StorageLocationType.Zone);
            var version = location.Version;

            var exception = Assert.Throws<DomainException>(() => location.Deactivate(activeChildCount: 2));

            Assert.Equal("Deactivate its 2 active sub-location(s) first.", exception.Message);
            Assert.True(location.IsActive);
            Assert.Equal(version, location.Version);
        }

        [Fact]
        public void Deactivate_WhenAlreadyInactive_ChangesNothing()
        {
            var location = Make(MakeWarehouse(), null, "A", StorageLocationType.Zone);
            location.Deactivate(activeChildCount: 0);
            var version = location.Version;

            location.Deactivate(activeChildCount: 5);   // already inactive: nothing to do, and no complaint

            Assert.False(location.IsActive);
            Assert.Equal(version, location.Version);
        }

        // ---------- reactivating ----------

        [Fact]
        public void Activate_UnderAnInactiveParent_Throws_AndStaysInactive()
        {
            var warehouse = MakeWarehouse();
            var zone = Make(warehouse, null, "A", StorageLocationType.Zone, id: 1);
            var aisle = Make(warehouse, zone, "A1", StorageLocationType.Aisle);
            aisle.Deactivate(activeChildCount: 0);
            zone.Deactivate(activeChildCount: 0);

            var exception = Assert.Throws<DomainException>(() => aisle.Activate(zone));

            Assert.Equal("The parent location is inactive. Reactivate it first.", exception.Message);
            Assert.False(aisle.IsActive);
        }

        [Fact]
        public void Activate_UnderAnActiveParent_Succeeds_AndChangesTheVersion()
        {
            var warehouse = MakeWarehouse();
            var zone = Make(warehouse, null, "A", StorageLocationType.Zone, id: 1);
            var aisle = Make(warehouse, zone, "A1", StorageLocationType.Aisle);
            aisle.Deactivate(activeChildCount: 0);
            var before = aisle.Version;

            aisle.Activate(zone);

            Assert.True(aisle.IsActive);
            Assert.NotEqual(before, aisle.Version);
        }

        [Fact]
        public void Activate_ATopLevelLocation_NeedsNoParent()
        {
            var zone = Make(MakeWarehouse(), null, "A", StorageLocationType.Zone);
            zone.Deactivate(activeChildCount: 0);

            zone.Activate(parent: null);

            Assert.True(zone.IsActive);
        }

        [Fact]
        public void Activate_WhenAlreadyActive_ChangesNothing()
        {
            var zone = Make(MakeWarehouse(), null, "A", StorageLocationType.Zone);
            var version = zone.Version;

            zone.Activate(parent: null);

            Assert.Equal(version, zone.Version);
        }

        [Fact]
        public void ABranchComesBackParentFirst_ThenItsChildren()
        {
            var warehouse = MakeWarehouse();
            var zone = Make(warehouse, null, "A", StorageLocationType.Zone, id: 1);
            var aisle = Make(warehouse, zone, "A1", StorageLocationType.Aisle);

            // Going away: children first (a parent with active children refuses).
            Assert.Throws<DomainException>(() => zone.Deactivate(activeChildCount: 1));
            aisle.Deactivate(activeChildCount: 0);
            zone.Deactivate(activeChildCount: 0);

            // Coming back: the parent first (a child under an inactive parent refuses).
            Assert.Throws<DomainException>(() => aisle.Activate(zone));
            zone.Activate(parent: null);
            aisle.Activate(zone);

            Assert.True(zone.IsActive);
            Assert.True(aisle.IsActive);
        }

        // ---------- tenancy ----------

        [Fact]
        public void StorageLocation_IsATenantEntity_OwnedByTheWarehousesOrganization()
        {
            var location = Make(MakeWarehouse(organizationId: 42), null, "A", StorageLocationType.Zone);

            Assert.IsAssignableFrom<ITenantEntity>(location);
            Assert.Equal(42, location.OrganizationId);
        }
    }
}
