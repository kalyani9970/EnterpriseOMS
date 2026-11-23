using EnterpriseOMS.Domain.Common;
using EnterpriseOMS.Domain.Common.Exceptions;
using EnterpriseOMS.Domain.Entities.Catalog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.UnitTests.Entities
{
    public class ProductTests
    {
        private static Product Make(string sku = "LAP-001") =>
        Product.Create(7, sku, "Laptop", "A laptop", "Electronics", UnitOfMeasure.Piece, 899.00m, 690.00m, 5);

        private static Product CreateWith(
            int organizationId = 1,
            string sku = "A",
            string name = "Name",
            string? description = null,
            string? category = null,
            UnitOfMeasure unit = UnitOfMeasure.Piece,
            decimal unitPrice = 1m,
            decimal costPrice = 1m,
            int reorderLevel = 1) =>
            Product.Create(organizationId, sku, name, description, category, unit, unitPrice, costPrice, reorderLevel);

        // ---------- Create: happy path ----------

        [Fact]
        public void Create_StoresTheDetails_AndStartsActive()
        {
            var product = Make();

            Assert.Equal(7, product.OrganizationId);
            Assert.Equal("Laptop", product.Name);
            Assert.Equal("A laptop", product.Description);
            Assert.Equal("Electronics", product.Category);
            Assert.Equal(UnitOfMeasure.Piece, product.UnitOfMeasure);
            Assert.Equal(899.00m, product.UnitPrice);
            Assert.Equal(690.00m, product.CostPrice);
            Assert.Equal(5, product.ReorderLevel);
            Assert.True(product.IsActive);
            Assert.NotEqual(Guid.Empty, product.Version);
        }

        [Fact]
        public void Create_KeepsTheSkuAsTyped_ButNormalizesItForUniqueness()
        {
            var product = Make("  lap-001  ");

            Assert.Equal("lap-001", product.Sku);
            Assert.Equal("LAP-001", product.NormalizedSku);
        }

        [Fact]
        public void NormalizeSku_TrimsAndUpperCases()
        {
            Assert.Equal("AB-1", Product.NormalizeSku("  ab-1 "));
        }

        [Fact]
        public void Create_BlankDescriptionAndCategory_BecomeNull()
        {
            var product = CreateWith(name: "  Pen  ", description: "   ", category: "");

            Assert.Equal("Pen", product.Name);
            Assert.Null(product.Description);
            Assert.Null(product.Category);
        }

        // ---------- Create: organization and SKU ----------

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Create_WithoutAValidOrganization_Throws(int organizationId)
        {
            var exception = Assert.Throws<DomainException>(() => CreateWith(organizationId: organizationId));

            Assert.Equal("A product must belong to an organization.", exception.Message);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_EmptySku_Throws(string sku)
        {
            var exception = Assert.Throws<DomainException>(() => CreateWith(sku: sku));

            Assert.Equal("SKU is required.", exception.Message);
        }

        [Fact]
        public void Create_SkuLongerThan50_Throws()
        {
            Assert.Throws<DomainException>(() => CreateWith(sku: new string('A', 51)));
        }

        [Fact]
        public void Create_SkuOfExactly50Characters_Succeeds()
        {
            Assert.Equal(50, CreateWith(sku: new string('A', 50)).Sku.Length);
        }

        [Theory]
        [InlineData("-abc")]    // must start with a letter or digit
        [InlineData("a b")]     // no spaces
        [InlineData("a#b")]
        [InlineData("ab*")]
        [InlineData("é1")]      // letters outside A-Z
        public void Create_SkuWithIllegalCharacters_Throws(string sku)
        {
            var exception = Assert.Throws<DomainException>(() => CreateWith(sku: sku));

            Assert.Contains("SKU may contain only", exception.Message);
        }

        [Theory]
        [InlineData("A")]
        [InlineData("a.b-c_d/e")]
        [InlineData("123")]
        [InlineData("LAP-001")]
        [InlineData("X/Y.2")]
        public void Create_ValidSku_IsAccepted(string sku)
        {
            Assert.Equal(sku, CreateWith(sku: sku).Sku);
        }

        // ---------- Create: text fields ----------

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_EmptyName_Throws(string name)
        {
            var exception = Assert.Throws<DomainException>(() => CreateWith(name: name));

            Assert.Equal("Product name is required.", exception.Message);
        }

        [Fact]
        public void Create_NameLongerThan200_Throws()
        {
            Assert.Throws<DomainException>(() => CreateWith(name: new string('x', 201)));
        }

        [Fact]
        public void Create_NameOfExactly200Characters_Succeeds()
        {
            Assert.Equal(200, CreateWith(name: new string('x', 200)).Name.Length);
        }

        [Fact]
        public void Create_DescriptionLongerThan2000_Throws()
        {
            Assert.Throws<DomainException>(() => CreateWith(description: new string('x', 2001)));
        }

        [Fact]
        public void Create_DescriptionOfExactly2000Characters_Succeeds()
        {
            Assert.Equal(2000, CreateWith(description: new string('x', 2000)).Description!.Length);
        }

        [Fact]
        public void Create_CategoryLongerThan100_Throws()
        {
            Assert.Throws<DomainException>(() => CreateWith(category: new string('x', 101)));
        }

        [Fact]
        public void Create_UndefinedUnitOfMeasure_Throws()
        {
            var exception = Assert.Throws<DomainException>(() => CreateWith(unit: (UnitOfMeasure)99));

            Assert.Equal("Unit of measure is not valid.", exception.Message);
        }

        // ---------- Create: numbers ----------

        [Fact]
        public void Create_NegativeUnitPrice_Throws()
        {
            var exception = Assert.Throws<DomainException>(() => CreateWith(unitPrice: -0.01m));

            Assert.Contains("Unit price", exception.Message);
        }

        [Fact]
        public void Create_NegativeCostPrice_Throws()
        {
            var exception = Assert.Throws<DomainException>(() => CreateWith(costPrice: -1m));

            Assert.Contains("Cost price", exception.Message);
        }

        [Fact]
        public void Create_PriceWithMoreThanTwoDecimals_Throws()
        {
            var exception = Assert.Throws<DomainException>(() => CreateWith(unitPrice: 1.005m));

            Assert.Contains("2 decimal places", exception.Message);
        }

        [Fact]
        public void Create_PriceAboveTheMaximum_Throws()
        {
            Assert.Throws<DomainException>(() => CreateWith(unitPrice: 1_000_000_000m));
        }

        [Fact]
        public void Create_PriceAtTheMaximumOrZero_Succeeds()
        {
            Assert.Equal(Product.MaxPrice, CreateWith(unitPrice: Product.MaxPrice).UnitPrice);
            Assert.Equal(0m, CreateWith(unitPrice: 0m, costPrice: 0m).UnitPrice);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(1_000_001)]
        public void Create_ReorderLevelOutOfRange_Throws(int reorderLevel)
        {
            var exception = Assert.Throws<DomainException>(() => CreateWith(reorderLevel: reorderLevel));

            Assert.Contains("Reorder level", exception.Message);
        }

        [Fact]
        public void Create_ReorderLevelAtTheLimits_Succeeds()
        {
            Assert.Equal(0, CreateWith(reorderLevel: 0).ReorderLevel);
            Assert.Equal(1_000_000, CreateWith(reorderLevel: 1_000_000).ReorderLevel);
        }

        // ---------- Update ----------

        [Fact]
        public void Update_ChangesTheDetails_ButNeverTheSkuOrOrganization()
        {
            var product = Make("lap-001");

            product.Update("  Laptop Pro ", "Faster", "Computers", UnitOfMeasure.Box, 999.00m, 700.00m, 8);

            Assert.Equal("Laptop Pro", product.Name);
            Assert.Equal("Faster", product.Description);
            Assert.Equal("Computers", product.Category);
            Assert.Equal(UnitOfMeasure.Box, product.UnitOfMeasure);
            Assert.Equal(999.00m, product.UnitPrice);
            Assert.Equal(700.00m, product.CostPrice);
            Assert.Equal(8, product.ReorderLevel);
            Assert.Equal("lap-001", product.Sku);
            Assert.Equal("LAP-001", product.NormalizedSku);
            Assert.Equal(7, product.OrganizationId);
        }

        [Fact]
        public void Update_GivesTheProductANewVersion()
        {
            var product = Make();
            var before = product.Version;

            product.Update("Laptop", null, null, UnitOfMeasure.Piece, 1m, 1m, 0);

            Assert.NotEqual(before, product.Version);
        }

        [Fact]
        public void Update_WithInvalidData_Throws_AndChangesNothing()
        {
            var product = Make();
            var version = product.Version;

            Assert.Throws<DomainException>(() => product.Update("", null, null, UnitOfMeasure.Piece, 1m, 1m, 1));

            Assert.Equal("Laptop", product.Name);
            Assert.Equal(899.00m, product.UnitPrice);
            Assert.Equal(version, product.Version);
        }

        [Fact]
        public void Update_WithOneBadField_DoesNotApplyTheGoodFields()
        {
            var product = Make();
            var version = product.Version;

            // A valid new name, but an invalid price: nothing may be applied, not even the name.
            Assert.Throws<DomainException>(() => product.Update("Changed", null, null, UnitOfMeasure.Piece, -5m, 1m, 1));

            Assert.Equal("Laptop", product.Name);
            Assert.Equal(version, product.Version);
        }

        // ---------- Activate / deactivate ----------

        [Fact]
        public void Deactivate_FlipsTheFlag_AndChangesTheVersion()
        {
            var product = Make();
            var before = product.Version;

            product.Deactivate();

            Assert.False(product.IsActive);
            Assert.NotEqual(before, product.Version);
        }

        [Fact]
        public void Deactivate_WhenAlreadyInactive_ChangesNothing()
        {
            var product = Make();
            product.Deactivate();
            var version = product.Version;

            product.Deactivate();

            Assert.False(product.IsActive);
            Assert.Equal(version, product.Version);
        }

        [Fact]
        public void Activate_FlipsTheFlagBack_AndChangesTheVersion()
        {
            var product = Make();
            product.Deactivate();
            var before = product.Version;

            product.Activate();

            Assert.True(product.IsActive);
            Assert.NotEqual(before, product.Version);
        }

        [Fact]
        public void Activate_WhenAlreadyActive_ChangesNothing()
        {
            var product = Make();
            var version = product.Version;

            product.Activate();

            Assert.True(product.IsActive);
            Assert.Equal(version, product.Version);
        }

        // ---------- Tenancy ----------

        [Fact]
        public void Product_IsATenantEntity()
        {
            Assert.IsAssignableFrom<ITenantEntity>(Make());
        }
    }
}
