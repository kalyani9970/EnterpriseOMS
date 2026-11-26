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
    /// <summary>Reserved stock: units promised to confirmed orders. The rule that never breaks: 0 &lt;= reserved &lt;= on hand.</summary>
    public class StockLevelReservationTests
    {
        private static StockLevel MakeLevel(int onHand)
        {
            var warehouse = Warehouse.Create(7, "WH", "Main", Address.Create("1 Test Street", null, "Utrecht", null, null, "NL"));
            warehouse.Id = 10;
            var product = Product.Create(7, "LAP-001", "Laptop", null, null, UnitOfMeasure.Piece, 10m, 5m, 5);
            product.Id = 3;

            var level = StockLevel.Create(product, warehouse);
            if (onHand > 0) level.Receive(onHand);
            return level;
        }

        [Fact]
        public void ANewLevel_HasNothingReserved_AndEverythingAvailable()
        {
            var level = MakeLevel(10);

            Assert.Equal(0, level.QuantityReserved);
            Assert.Equal(10, level.QuantityAvailable);
        }

        // ---------- Reserve ----------

        [Fact]
        public void Reserve_HoldsUnitsBack_WithoutTouchingWhatIsOnHand()
        {
            var level = MakeLevel(10);
            var version = level.Version;

            level.Reserve(4);

            Assert.Equal(10, level.QuantityOnHand);
            Assert.Equal(4, level.QuantityReserved);
            Assert.Equal(6, level.QuantityAvailable);
            Assert.NotEqual(version, level.Version);
        }

        [Fact]
        public void Reserve_Everything_IsAllowed_AndLeavesNothingAvailable()
        {
            var level = MakeLevel(10);

            level.Reserve(10);

            Assert.Equal(0, level.QuantityAvailable);
        }

        [Fact]
        public void Reserve_MoreThanIsAvailable_Throws_WithTheNumbers_AndChangesNothing()
        {
            var level = MakeLevel(10);
            var version = level.Version;

            var exception = Assert.Throws<DomainException>(() => level.Reserve(11));

            Assert.Equal("Not enough available stock to reserve: 10 available, 11 requested.", exception.Message);
            Assert.Equal(0, level.QuantityReserved);
            Assert.Equal(version, level.Version);
        }

        [Fact]
        public void Reserve_CountsAgainstWhatIsAlreadyReserved()
        {
            var level = MakeLevel(10);
            level.Reserve(6);

            var exception = Assert.Throws<DomainException>(() => level.Reserve(5));

            Assert.Equal("Not enough available stock to reserve: 4 available, 5 requested.", exception.Message);
            Assert.Equal(6, level.QuantityReserved);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Reserve_ANonPositiveQuantity_Throws(int quantity)
        {
            var exception = Assert.Throws<DomainException>(() => MakeLevel(10).Reserve(quantity));

            Assert.Equal("Quantity must be greater than zero.", exception.Message);
        }

        // ---------- Release ----------

        [Fact]
        public void Release_GivesUnitsBack_SoTheyAreAvailableAgain()
        {
            var level = MakeLevel(10);
            level.Reserve(6);
            var version = level.Version;

            level.Release(4);

            Assert.Equal(2, level.QuantityReserved);
            Assert.Equal(8, level.QuantityAvailable);
            Assert.Equal(10, level.QuantityOnHand);
            Assert.NotEqual(version, level.Version);
        }

        [Fact]
        public void Release_MoreThanIsReserved_Throws_AndChangesNothing()
        {
            var level = MakeLevel(10);
            level.Reserve(3);
            var version = level.Version;

            var exception = Assert.Throws<DomainException>(() => level.Release(4));

            Assert.Equal("Cannot release more than is reserved: 3 reserved, 4 requested.", exception.Message);
            Assert.Equal(3, level.QuantityReserved);
            Assert.Equal(version, level.Version);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-2)]
        public void Release_ANonPositiveQuantity_Throws(int quantity)
        {
            var level = MakeLevel(10);
            level.Reserve(3);

            Assert.Throws<DomainException>(() => level.Release(quantity));
        }

        // ---------- IssueReserved ----------

        [Fact]
        public void IssueReserved_SendsUnitsOut_DroppingBothReservedAndOnHand_AndReturnsTheLedgerLine()
        {
            var level = MakeLevel(10);
            level.Reserve(5);

            var movement = level.IssueReserved(3, "SO-000001", "Fulfilment of order SO-000001");

            Assert.Equal(7, level.QuantityOnHand);
            Assert.Equal(2, level.QuantityReserved);
            Assert.Equal(StockMovementType.Issue, movement.Type);
            Assert.Equal(-3, movement.QuantityChange);
            Assert.Equal(7, movement.QuantityAfter);
            Assert.Equal("SO-000001", movement.Reference);
            Assert.Equal("Fulfilment of order SO-000001", movement.Notes);
        }

        [Fact]
        public void IssueReserved_LeavesWhatIsAvailableToOthersExactlyAsItWas()
        {
            var level = MakeLevel(10);
            level.Reserve(5);
            var availableBefore = level.QuantityAvailable;

            level.IssueReserved(3);

            Assert.Equal(availableBefore, level.QuantityAvailable);
        }

        [Fact]
        public void IssueReserved_MoreThanIsReserved_Throws_AndChangesNothing()
        {
            var level = MakeLevel(10);
            level.Reserve(2);
            var version = level.Version;

            var exception = Assert.Throws<DomainException>(() => level.IssueReserved(3));

            Assert.Equal("Cannot issue more than is reserved: 2 reserved, 3 requested.", exception.Message);
            Assert.Equal(10, level.QuantityOnHand);
            Assert.Equal(2, level.QuantityReserved);
            Assert.Equal(version, level.Version);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void IssueReserved_ANonPositiveQuantity_Throws(int quantity)
        {
            var level = MakeLevel(10);
            level.Reserve(5);

            Assert.Throws<DomainException>(() => level.IssueReserved(quantity));
        }

        [Fact]
        public void IssueReserved_AReferenceThatIsTooLong_Throws_AndChangesNothing()
        {
            var level = MakeLevel(10);
            level.Reserve(5);

            Assert.Throws<DomainException>(() => level.IssueReserved(1, new string('x', 101)));

            Assert.Equal(10, level.QuantityOnHand);
            Assert.Equal(5, level.QuantityReserved);
        }

        // ---------- the manual operations now respect reservations ----------

        [Fact]
        public void Issue_CannotTakeReservedUnits_AndSaysHowMuchIsReserved()
        {
            var level = MakeLevel(10);
            level.Reserve(7);
            var version = level.Version;

            var exception = Assert.Throws<DomainException>(() => level.Issue(4));

            Assert.Equal("Not enough available stock: 10 on hand, 7 reserved for orders, 4 requested.", exception.Message);
            Assert.Equal(10, level.QuantityOnHand);
            Assert.Equal(version, level.Version);
        }

        [Fact]
        public void Issue_UpToTheAvailableQuantity_IsAllowed()
        {
            var level = MakeLevel(10);
            level.Reserve(7);

            level.Issue(3);

            Assert.Equal(7, level.QuantityOnHand);
            Assert.Equal(7, level.QuantityReserved);
            Assert.Equal(0, level.QuantityAvailable);
        }

        [Fact]
        public void Issue_WithNothingReserved_KeepsTheOriginalMessage()
        {
            var level = MakeLevel(7);

            var exception = Assert.Throws<DomainException>(() => level.Issue(8));

            Assert.Equal("Not enough stock: 7 on hand, 8 requested.", exception.Message);
        }

        [Fact]
        public void Adjust_CannotCountBelowWhatIsReserved()
        {
            var level = MakeLevel(10);
            level.Reserve(7);
            var version = level.Version;

            var exception = Assert.Throws<DomainException>(() => level.Adjust(6, "Cycle count"));

            Assert.Equal("7 unit(s) are reserved for orders, so the counted quantity cannot be below that.", exception.Message);
            Assert.Equal(10, level.QuantityOnHand);
            Assert.Equal(version, level.Version);
        }

        [Fact]
        public void Adjust_ToExactlyWhatIsReserved_IsAllowed()
        {
            var level = MakeLevel(10);
            level.Reserve(7);

            var movement = level.Adjust(7, "Cycle count");

            Assert.Equal(7, level.QuantityOnHand);
            Assert.Equal(-3, movement.QuantityChange);
            Assert.Equal(0, level.QuantityAvailable);
        }

        [Fact]
        public void Receive_AddsToTheAvailableQuantity_NotToTheReservedOne()
        {
            var level = MakeLevel(10);
            level.Reserve(7);

            level.Receive(5);

            Assert.Equal(15, level.QuantityOnHand);
            Assert.Equal(7, level.QuantityReserved);
            Assert.Equal(8, level.QuantityAvailable);
        }

        [Fact]
        public void WhateverHappens_ReservedNeverGoesBelowZeroOrAboveWhatIsOnHand()
        {
            var level = MakeLevel(10);
            var steps = new List<Action>
        {
            () => level.Reserve(4),
            () => level.Issue(6),
            () => level.Reserve(1),            // refused: nothing available
            () => level.Receive(5),
            () => level.Reserve(5),
            () => level.Adjust(9, "count"),    // refused: below reserved? 9 < 9 is false, so allowed
            () => level.Release(3),
            () => level.IssueReserved(2),
            () => level.Adjust(1, "count"),    // refused: below reserved
            () => level.Release(6)             // refused: more than reserved
        };

            foreach (var step in steps)
            {
                try { step(); } catch (DomainException) { /* refused calls must leave things valid too */ }

                Assert.True(level.QuantityReserved >= 0);
                Assert.True(level.QuantityReserved <= level.QuantityOnHand);
                Assert.True(level.QuantityOnHand >= 0);
            }
        }
    }
}
