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
    public class WarehouseTests
    {
        private static Address SomeAddress(string city = "Pune") =>
        Address.Create("12 Industrial Road", null, city, "MH", "411001", "IN");

        private static Warehouse Make(string code = "WH-MAIN") =>
            Warehouse.Create(7, code, "Main Warehouse", SomeAddress());

        // ---------- Create ----------

        [Fact]
        public void Create_StoresTheDetails_AndStartsActive()
        {
            var warehouse = Warehouse.Create(7, "  wh-main ", "  Main Warehouse ", SomeAddress());

            Assert.Equal(7, warehouse.OrganizationId);
            Assert.Equal("wh-main", warehouse.Code);
            Assert.Equal("WH-MAIN", warehouse.NormalizedCode);
            Assert.Equal("Main Warehouse", warehouse.Name);
            Assert.Equal(SomeAddress(), warehouse.Address);
            Assert.True(warehouse.IsActive);
            Assert.NotEqual(Guid.Empty, warehouse.Version);
        }

        [Fact]
        public void NormalizeCode_TrimsAndUpperCases()
        {
            Assert.Equal("WH-1", Warehouse.NormalizeCode("  wh-1 "));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Create_WithoutAValidOrganization_Throws(int organizationId)
        {
            var exception = Assert.Throws<DomainException>(() => Warehouse.Create(organizationId, "A", "Name", SomeAddress()));

            Assert.Equal("A warehouse must belong to an organization.", exception.Message);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_EmptyCode_Throws(string code)
        {
            var exception = Assert.Throws<DomainException>(() => Warehouse.Create(1, code, "Name", SomeAddress()));

            Assert.Equal("Warehouse code is required.", exception.Message);
        }

        [Fact]
        public void Create_CodeLongerThan30_Throws()
        {
            Assert.Throws<DomainException>(() => Warehouse.Create(1, new string('A', 31), "Name", SomeAddress()));
        }

        [Fact]
        public void Create_CodeOfExactly30Characters_Succeeds()
        {
            Assert.Equal(30, Warehouse.Create(1, new string('A', 30), "Name", SomeAddress()).Code.Length);
        }

        [Theory]
        [InlineData("-x")]
        [InlineData("a b")]
        [InlineData("a#b")]
        [InlineData("é1")]
        public void Create_CodeWithIllegalCharacters_Throws(string code)
        {
            var exception = Assert.Throws<DomainException>(() => Warehouse.Create(1, code, "Name", SomeAddress()));

            Assert.Contains("may contain only", exception.Message);
        }

        [Theory]
        [InlineData("A")]
        [InlineData("wh-main")]
        [InlineData("W.1/2")]
        [InlineData("123")]
        public void Create_ValidCode_IsAccepted(string code)
        {
            Assert.Equal(code, Warehouse.Create(1, code, "Name", SomeAddress()).Code);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_EmptyName_Throws(string name)
        {
            var exception = Assert.Throws<DomainException>(() => Warehouse.Create(1, "A", name, SomeAddress()));

            Assert.Equal("Warehouse name is required.", exception.Message);
        }

        [Fact]
        public void Create_NameLongerThan200_Throws()
        {
            Assert.Throws<DomainException>(() => Warehouse.Create(1, "A", new string('x', 201), SomeAddress()));
        }

        [Fact]
        public void Create_NameOfExactly200Characters_Succeeds()
        {
            Assert.Equal(200, Warehouse.Create(1, "A", new string('x', 200), SomeAddress()).Name.Length);
        }

        [Fact]
        public void Create_WithoutAnAddress_Throws()
        {
            var exception = Assert.Throws<DomainException>(() => Warehouse.Create(1, "A", "Name", null!));

            Assert.Equal("A warehouse needs an address.", exception.Message);
        }

        // ---------- Update ----------

        [Fact]
        public void Update_ChangesTheNameAndAddress_ButNeverTheCode()
        {
            var warehouse = Make("wh-main");

            warehouse.Update("  Renamed  ", SomeAddress("Mumbai"));

            Assert.Equal("Renamed", warehouse.Name);
            Assert.Equal("Mumbai", warehouse.Address.City);
            Assert.Equal("wh-main", warehouse.Code);
            Assert.Equal("WH-MAIN", warehouse.NormalizedCode);
            Assert.Equal(7, warehouse.OrganizationId);
        }

        [Fact]
        public void Update_GivesTheWarehouseANewVersion()
        {
            var warehouse = Make();
            var before = warehouse.Version;

            warehouse.Update("Renamed", SomeAddress());

            Assert.NotEqual(before, warehouse.Version);
        }

        [Fact]
        public void Update_WithAnInvalidName_Throws_AndChangesNothing()
        {
            var warehouse = Make();
            var version = warehouse.Version;

            Assert.Throws<DomainException>(() => warehouse.Update("", SomeAddress("Mumbai")));

            Assert.Equal("Main Warehouse", warehouse.Name);
            Assert.Equal("Pune", warehouse.Address.City);
            Assert.Equal(version, warehouse.Version);
        }

        [Fact]
        public void Update_WithoutAnAddress_Throws_AndChangesNothing()
        {
            var warehouse = Make();

            Assert.Throws<DomainException>(() => warehouse.Update("New name", null!));

            Assert.Equal("Main Warehouse", warehouse.Name);
        }

        // ---------- Activate / deactivate ----------

        [Fact]
        public void Deactivate_FlipsTheFlag_AndChangesTheVersion()
        {
            var warehouse = Make();
            var before = warehouse.Version;

            warehouse.Deactivate();

            Assert.False(warehouse.IsActive);
            Assert.NotEqual(before, warehouse.Version);
        }

        [Fact]
        public void Deactivate_WhenAlreadyInactive_ChangesNothing()
        {
            var warehouse = Make();
            warehouse.Deactivate();
            var version = warehouse.Version;

            warehouse.Deactivate();

            Assert.Equal(version, warehouse.Version);
        }

        [Fact]
        public void Activate_FlipsTheFlagBack_AndChangesTheVersion()
        {
            var warehouse = Make();
            warehouse.Deactivate();
            var before = warehouse.Version;

            warehouse.Activate();

            Assert.True(warehouse.IsActive);
            Assert.NotEqual(before, warehouse.Version);
        }

        [Fact]
        public void Activate_WhenAlreadyActive_ChangesNothing()
        {
            var warehouse = Make();
            var version = warehouse.Version;

            warehouse.Activate();

            Assert.Equal(version, warehouse.Version);
        }

        [Fact]
        public void Warehouse_IsATenantEntity()
        {
            Assert.IsAssignableFrom<ITenantEntity>(Make());
        }

        // ---------- Deactivate while the warehouse holds stock ----------

        [Fact]
        public void Deactivate_WhileTheWarehouseStillHoldsStock_Throws_AndStaysActive()
        {
            var warehouse = Make();
            var version = warehouse.Version;

            var exception = Assert.Throws<DomainException>(() => warehouse.Deactivate(stockedProductCount: 3));

            Assert.Equal("This warehouse still holds stock of 3 product(s). Issue or move it out first.", exception.Message);
            Assert.True(warehouse.IsActive);
            Assert.Equal(version, warehouse.Version);
        }

        [Fact]
        public void Deactivate_WithNoStockHeld_Succeeds()
        {
            var warehouse = Make();
            var version = warehouse.Version;

            warehouse.Deactivate(stockedProductCount: 0);

            Assert.False(warehouse.IsActive);
            Assert.NotEqual(version, warehouse.Version);
        }

        [Fact]
        public void Deactivate_WithoutTheCount_StillWorks_SoExistingCallersAreNotBroken()
        {
            var warehouse = Make();

            warehouse.Deactivate();

            Assert.False(warehouse.IsActive);
        }

        [Fact]
        public void Deactivate_WhenAlreadyInactive_ChangesNothing_EvenIfStockIsReported()
        {
            var warehouse = Make();
            warehouse.Deactivate();
            var version = warehouse.Version;

            warehouse.Deactivate(stockedProductCount: 5);

            Assert.False(warehouse.IsActive);
            Assert.Equal(version, warehouse.Version);
        }
    }
}
