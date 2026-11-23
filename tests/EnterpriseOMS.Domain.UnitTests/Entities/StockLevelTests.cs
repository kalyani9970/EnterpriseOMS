using EnterpriseOMS.Domain.Common;
using EnterpriseOMS.Domain.Common.Exceptions;
using EnterpriseOMS.Domain.Entities.Catalog;
using EnterpriseOMS.Domain.Entities.Inventory;
using EnterpriseOMS.Domain.Entities.Warehousing;
using EnterpriseOMS.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.UnitTests.Entities
{
    public class StockLevelTests
    {
        private static Warehouse MakeWarehouse(int organizationId = 7, int id = 10)
        {
            var warehouse = Warehouse.Create(
                organizationId, "WH", "Main", Address.Create("1 Test Street", null, "Utrecht", null, null, "NL"));
            warehouse.Id = id;   // EF assigns the id when a real warehouse is saved; tests set it by hand
            return warehouse;
        }

        private static Product MakeProduct(int organizationId = 7, int id = 3)
        {
            var product = Product.Create(organizationId, "LAP-001", "Laptop", null, null, UnitOfMeasure.Piece, 10m, 5m, 5);
            product.Id = id;
            return product;
        }

        private static StockLevel MakeLevel() => StockLevel.Create(MakeProduct(), MakeWarehouse());

        private static StockLevel MakeLevelWith(int quantity)
        {
            var level = MakeLevel();
            level.Receive(quantity);
            return level;
        }

        // ---------- Create ----------

        [Fact]
        public void Create_StartsEmpty_ForTheGivenProductAndWarehouse()
        {
            var level = MakeLevel();

            Assert.Equal(0, level.QuantityOnHand);
            Assert.Equal(7, level.OrganizationId);
            Assert.Equal(3, level.ProductId);
            Assert.Equal(10, level.WarehouseId);
            Assert.NotEqual(Guid.Empty, level.Version);
        }

        [Fact]
        public void Create_ForAProductThatIsNotSavedYet_Throws()
        {
            var exception = Assert.Throws<DomainException>(() => StockLevel.Create(MakeProduct(id: 0), MakeWarehouse()));

            Assert.Contains("must be saved", exception.Message);
        }

        [Fact]
        public void Create_ForAWarehouseThatIsNotSavedYet_Throws()
        {
            var exception = Assert.Throws<DomainException>(() => StockLevel.Create(MakeProduct(), MakeWarehouse(id: 0)));

            Assert.Contains("must be saved", exception.Message);
        }

        [Fact]
        public void Create_ForAProductAndWarehouseOfDifferentOrganizations_Throws()
        {
            var exception = Assert.Throws<DomainException>(
                () => StockLevel.Create(MakeProduct(organizationId: 7), MakeWarehouse(organizationId: 8)));

            Assert.Contains("different organizations", exception.Message);
        }

        [Fact]
        public void StockLevel_IsATenantEntity()
        {
            Assert.IsAssignableFrom<ITenantEntity>(MakeLevel());
        }

        // ---------- Receive ----------

        [Fact]
        public void Receive_RaisesTheQuantity_AndGivesANewVersion()
        {
            var level = MakeLevel();
            var before = level.Version;

            level.Receive(10);

            Assert.Equal(10, level.QuantityOnHand);
            Assert.NotEqual(before, level.Version);
        }

        [Fact]
        public void Receive_ReturnsTheLedgerLineThatRecordsIt()
        {
            var level = MakeLevel();

            var movement = level.Receive(10);

            Assert.Equal(StockMovementType.Receipt, movement.Type);
            Assert.Equal(10, movement.QuantityChange);
            Assert.Equal(10, movement.QuantityAfter);
            Assert.Equal(7, movement.OrganizationId);
            Assert.Equal(3, movement.ProductId);
            Assert.Equal(10, movement.WarehouseId);
        }

        [Fact]
        public void Receive_TrimsTheReferenceAndNotes()
        {
            var movement = MakeLevel().Receive(1, "  PO-1  ", "  first delivery ");

            Assert.Equal("PO-1", movement.Reference);
            Assert.Equal("first delivery", movement.Notes);
        }

        [Fact]
        public void Receive_BlankReferenceAndNotes_BecomeNull()
        {
            var movement = MakeLevel().Receive(1, "   ", "");

            Assert.Null(movement.Reference);
            Assert.Null(movement.Notes);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void Receive_AQuantityThatIsNotPositive_Throws(int quantity)
        {
            var exception = Assert.Throws<DomainException>(() => MakeLevel().Receive(quantity));

            Assert.Equal("Quantity must be greater than zero.", exception.Message);
        }

        [Fact]
        public void Receive_AReferenceLongerThan100_Throws_AndChangesNothing()
        {
            var level = MakeLevelWith(5);
            var version = level.Version;

            Assert.Throws<DomainException>(() => level.Receive(1, new string('x', 101)));

            Assert.Equal(5, level.QuantityOnHand);
            Assert.Equal(version, level.Version);
        }

        [Fact]
        public void Receive_NotesLongerThan500_Throws_AndChangesNothing()
        {
            var level = MakeLevelWith(5);
            var version = level.Version;

            Assert.Throws<DomainException>(() => level.Receive(1, null, new string('x', 501)));

            Assert.Equal(5, level.QuantityOnHand);
            Assert.Equal(version, level.Version);
        }

        [Fact]
        public void Receive_ReferenceAndNotesAtTheirLimits_AreAccepted()
        {
            var movement = MakeLevel().Receive(1, new string('x', 100), new string('y', 500));

            Assert.Equal(100, movement.Reference!.Length);
            Assert.Equal(500, movement.Notes!.Length);
        }

        [Fact]
        public void Receive_CanReachTheCeiling_ButNotPassIt()
        {
            var level = MakeLevel();
            level.Receive(StockLevel.MaxQuantity);

            Assert.Equal(1_000_000_000, level.QuantityOnHand);

            var exception = Assert.Throws<DomainException>(() => level.Receive(1));

            Assert.Contains("1,000,000,000", exception.Message);
            Assert.Equal(1_000_000_000, level.QuantityOnHand);
        }

        [Fact]
        public void Receive_AHugeQuantity_IsRefusedInsteadOfOverflowing()
        {
            var level = MakeLevelWith(5);

            // 5 + int.MaxValue would wrap around to a NEGATIVE int if the sum were not checked in a wider type.
            Assert.Throws<DomainException>(() => level.Receive(int.MaxValue));

            Assert.Equal(5, level.QuantityOnHand);
        }

        // ---------- Issue ----------

        [Fact]
        public void Issue_LowersTheQuantity_AndRecordsANegativeChange()
        {
            var level = MakeLevelWith(10);
            var before = level.Version;

            var movement = level.Issue(3);

            Assert.Equal(7, level.QuantityOnHand);
            Assert.NotEqual(before, level.Version);
            Assert.Equal(StockMovementType.Issue, movement.Type);
            Assert.Equal(-3, movement.QuantityChange);
            Assert.Equal(7, movement.QuantityAfter);
        }

        [Fact]
        public void Issue_KeepsTheReferenceAndNotes()
        {
            var movement = MakeLevelWith(10).Issue(3, "SO-1", "to a customer");

            Assert.Equal("SO-1", movement.Reference);
            Assert.Equal("to a customer", movement.Notes);
        }

        [Fact]
        public void Issue_MoreThanIsOnHand_Throws_WithTheNumbers_AndChangesNothing()
        {
            var level = MakeLevelWith(7);
            var version = level.Version;

            var exception = Assert.Throws<DomainException>(() => level.Issue(8));

            Assert.Equal("Not enough stock: 7 on hand, 8 requested.", exception.Message);
            Assert.Equal(7, level.QuantityOnHand);
            Assert.Equal(version, level.Version);
        }

        [Fact]
        public void Issue_Everything_IsAllowed_AndLeavesZero()
        {
            var level = MakeLevelWith(7);

            var movement = level.Issue(7);

            Assert.Equal(0, level.QuantityOnHand);
            Assert.Equal(0, movement.QuantityAfter);
        }

        [Fact]
        public void Issue_FromEmptyStock_Throws()
        {
            var exception = Assert.Throws<DomainException>(() => MakeLevel().Issue(1));

            Assert.Equal("Not enough stock: 0 on hand, 1 requested.", exception.Message);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void Issue_AQuantityThatIsNotPositive_Throws(int quantity)
        {
            var exception = Assert.Throws<DomainException>(() => MakeLevelWith(10).Issue(quantity));

            Assert.Equal("Quantity must be greater than zero.", exception.Message);
        }

        [Fact]
        public void Issue_AReferenceLongerThan100_Throws_AndChangesNothing()
        {
            var level = MakeLevelWith(5);
            var version = level.Version;

            Assert.Throws<DomainException>(() => level.Issue(1, new string('x', 101)));

            Assert.Equal(5, level.QuantityOnHand);
            Assert.Equal(version, level.Version);
        }

        // ---------- Adjust (a stock count) ----------

        [Fact]
        public void Adjust_ACountAboveTheSystemsNumber_RecordsAPositiveChange()
        {
            var level = MakeLevelWith(10);

            var movement = level.Adjust(13, "found 3 more in the back room");

            Assert.Equal(13, level.QuantityOnHand);
            Assert.Equal(StockMovementType.Adjustment, movement.Type);
            Assert.Equal(3, movement.QuantityChange);
            Assert.Equal(13, movement.QuantityAfter);
        }

        [Fact]
        public void Adjust_ACountBelowTheSystemsNumber_RecordsANegativeChange()
        {
            var level = MakeLevelWith(13);

            var movement = level.Adjust(9, "2 damaged, 2 missing");

            Assert.Equal(9, level.QuantityOnHand);
            Assert.Equal(-4, movement.QuantityChange);
            Assert.Equal(9, movement.QuantityAfter);
        }

        [Fact]
        public void Adjust_CanCountZero()
        {
            var level = MakeLevelWith(9);

            var movement = level.Adjust(0, "warehouse emptied");

            Assert.Equal(0, level.QuantityOnHand);
            Assert.Equal(-9, movement.QuantityChange);
        }

        [Fact]
        public void Adjust_StoresTheReasonAsTheNotes_TrimmedAndWithoutAReference()
        {
            var movement = MakeLevelWith(10).Adjust(12, "  cycle count  ");

            Assert.Equal("cycle count", movement.Notes);
            Assert.Null(movement.Reference);
        }

        [Fact]
        public void Adjust_ToTheSameNumberAsTheSystem_Throws_BecauseThereIsNothingToCorrect()
        {
            var level = MakeLevelWith(10);
            var version = level.Version;

            var exception = Assert.Throws<DomainException>(() => level.Adjust(10, "recount"));

            Assert.Contains("Nothing to adjust", exception.Message);
            Assert.Equal(version, level.Version);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Adjust_WithoutAReason_Throws(string reason)
        {
            var exception = Assert.Throws<DomainException>(() => MakeLevelWith(10).Adjust(12, reason));

            Assert.Equal("A reason is required for a stock adjustment.", exception.Message);
        }

        [Fact]
        public void Adjust_ANegativeCount_Throws()
        {
            var exception = Assert.Throws<DomainException>(() => MakeLevelWith(10).Adjust(-1, "x"));

            Assert.Contains("cannot be negative", exception.Message);
        }

        [Fact]
        public void Adjust_ACountAboveTheCeiling_Throws()
        {
            var exception = Assert.Throws<DomainException>(() => MakeLevelWith(10).Adjust(StockLevel.MaxQuantity + 1, "x"));

            Assert.Contains("1,000,000,000", exception.Message);
        }

        [Fact]
        public void Adjust_AReasonLongerThan500_Throws()
        {
            var exception = Assert.Throws<DomainException>(() => MakeLevelWith(10).Adjust(12, new string('x', 501)));

            Assert.Contains("500", exception.Message);
        }

        [Fact]
        public void Adjust_ARefusedCount_LeavesTheLevelUntouched()
        {
            var level = MakeLevelWith(10);
            var version = level.Version;

            Assert.Throws<DomainException>(() => level.Adjust(12, "   "));
            Assert.Throws<DomainException>(() => level.Adjust(-3, "x"));
            Assert.Throws<DomainException>(() => level.Adjust(10, "same"));

            Assert.Equal(10, level.QuantityOnHand);
            Assert.Equal(version, level.Version);
        }

        // ---------- the ledger as a whole ----------

        [Fact]
        public void TheLedgerReadsAsARunningBalance()
        {
            var level = MakeLevel();

            var lines = new[]
            {
            level.Receive(10),
            level.Issue(3),
            level.Adjust(6, "count"),
            level.Receive(4)
        };

            Assert.Equal(10, lines[0].QuantityAfter);
            Assert.Equal(7, lines[1].QuantityAfter);
            Assert.Equal(6, lines[2].QuantityAfter);
            Assert.Equal(10, lines[3].QuantityAfter);
        }

        [Fact]
        public void TheSumOfTheChanges_AlwaysEqualsTheQuantityOnHand()
        {
            var level = MakeLevel();

            var lines = new[]
            {
            level.Receive(10),
            level.Issue(3),
            level.Adjust(6, "count"),
            level.Receive(4)
        };

            Assert.Equal(level.QuantityOnHand, lines.Sum(line => line.QuantityChange));
        }

        [Fact]
        public void EveryLedgerLine_CarriesItsOwnersIdentity_AndIsATenantEntity()
        {
            var movement = MakeLevel().Receive(1);

            Assert.IsAssignableFrom<ITenantEntity>(movement);
            Assert.Equal(7, movement.OrganizationId);
            Assert.Equal(3, movement.ProductId);
            Assert.Equal(10, movement.WarehouseId);
        }

        [Fact]
        public void MovementTypes_HaveStableNumbers_BecauseTheyArePartOfTheStoredAndSentData()
        {
            Assert.Equal(1, (int)StockMovementType.Receipt);
            Assert.Equal(2, (int)StockMovementType.Issue);
            Assert.Equal(3, (int)StockMovementType.Adjustment);
        }

        // ---------- stock of inactive things can still be cleared out ----------

        [Fact]
        public void ExistingStockOfAnInactiveProduct_CanStillBeIssuedAndCounted()
        {
            var product = MakeProduct();
            var level = StockLevel.Create(product, MakeWarehouse());
            level.Receive(5);
            product.Deactivate();

            var issue = level.Issue(2);
            var count = level.Adjust(1, "count");

            Assert.Equal(3, issue.QuantityAfter);
            Assert.Equal(1, count.QuantityAfter);
        }
    }
}
