using EnterpriseOMS.Domain.Common;
using EnterpriseOMS.Domain.Common.Exceptions;
using EnterpriseOMS.Domain.Entities.Catalog;
using EnterpriseOMS.Domain.Entities.Warehousing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.Entities.Inventory
{
    /// <summary>
    /// How many units of ONE product are on hand in ONE warehouse, and how many of those are RESERVED for
    /// confirmed orders. What can still be promised or taken is the AVAILABLE quantity (on hand minus reserved).
    /// The quantities can only change through Receive, Issue and Adjust (physical changes, each returning the
    /// ledger line that records it) and Reserve, Release and IssueReserved (the order lifecycle). The rule that
    /// ties it together never breaks: 0 &lt;= reserved &lt;= on hand. The Version token makes two people changing the
    /// same stock at the same moment safe: the database rejects the second save instead of losing one change.
    /// </summary>
    public class StockLevel : BaseAuditableEntity, IAggregateRoot, ITenantEntity
    {
        /// <summary>A sanity ceiling that also keeps the quantity safely inside an int.</summary>
        public const int MaxQuantity = 1_000_000_000;

        // Required by EF Core. All other code must go through Create(), which enforces the rules.
        private StockLevel() { }

        public int OrganizationId { get; private set; }
        public int ProductId { get; private set; }
        public int WarehouseId { get; private set; }

        /// <summary>Whole units physically in the warehouse. Never negative.</summary>
        public int QuantityOnHand { get; private set; }

        /// <summary>Units promised to confirmed orders. Still physically here, but not available to anyone else.</summary>
        public int QuantityReserved { get; private set; }

        /// <summary>What can still be promised to an order or taken out by hand: on hand minus reserved.</summary>
        public int QuantityAvailable => QuantityOnHand - QuantityReserved;

        /// <summary>Optimistic-concurrency token: a new value on every change.</summary>
        public Guid Version { get; private set; } = Guid.NewGuid();

        /// <summary>Starts an empty level for a product in a warehouse. Both must already be saved and belong to the same organization.</summary>
        public static StockLevel Create(Product product, Warehouse warehouse)
        {
            if (product.Id <= 0 || warehouse.Id <= 0)
                throw new DomainException("The product and the warehouse must be saved before stock can be recorded.");

            StockRules.EnsureSameOrganization(product, warehouse);

            return new StockLevel
            {
                OrganizationId = product.OrganizationId,
                ProductId = product.Id,
                WarehouseId = warehouse.Id,
                QuantityOnHand = 0
            };
        }

        /// <summary>Stock came in. Returns the ledger line to save together with this level.</summary>
        public StockMovement Receive(int quantity, string? reference = null, string? notes = null)
        {
            // Validate everything first, then change anything: a rejected call must leave the level untouched.
            RequirePositive(quantity);
            var cleanReference = StockMovement.CleanText(reference, "Reference", StockMovement.ReferenceMaxLength);
            var cleanNotes = StockMovement.CleanText(notes, "Notes", StockMovement.NotesMaxLength);

            if ((long)QuantityOnHand + quantity > MaxQuantity)
                throw new DomainException("Stock cannot exceed 1,000,000,000 units.");

            QuantityOnHand += quantity;
            Version = Guid.NewGuid();

            return StockMovement.Create(this, StockMovementType.Receipt, quantity, cleanReference, cleanNotes);
        }

        /// <summary>
        /// Stock went out by hand. Only AVAILABLE stock can be taken: units reserved for orders are not up for grabs.
        /// Returns the ledger line to save together with this level.
        /// </summary>
        public StockMovement Issue(int quantity, string? reference = null, string? notes = null)
        {
            RequirePositive(quantity);
            var cleanReference = StockMovement.CleanText(reference, "Reference", StockMovement.ReferenceMaxLength);
            var cleanNotes = StockMovement.CleanText(notes, "Notes", StockMovement.NotesMaxLength);

            if (quantity > QuantityAvailable)
            {
                throw new DomainException(
                    QuantityReserved == 0
                        ? $"Not enough stock: {QuantityOnHand} on hand, {quantity} requested."
                        : $"Not enough available stock: {QuantityOnHand} on hand, {QuantityReserved} reserved for orders, {quantity} requested.");
            }

            QuantityOnHand -= quantity;
            Version = Guid.NewGuid();

            return StockMovement.Create(this, StockMovementType.Issue, -quantity, cleanReference, cleanNotes);
        }

        /// <summary>
        /// A stock count: sets the quantity to what was actually counted. The ledger records the difference,
        /// either sign, with the (required) reason. A count cannot go below what is reserved for orders (those
        /// units are promised, so the shortfall must be sorted out on the orders first). Counting the same number
        /// as the system has is refused, because there is nothing to correct.
        /// </summary>
        public StockMovement Adjust(int countedQuantity, string reason)
        {
            if (countedQuantity < 0)
                throw new DomainException("The counted quantity cannot be negative.");

            if (countedQuantity > MaxQuantity)
                throw new DomainException("Stock cannot exceed 1,000,000,000 units.");

            var cleanReason = StockMovement.CleanText(reason, "Reason", StockMovement.NotesMaxLength);
            if (cleanReason is null)
                throw new DomainException("A reason is required for a stock adjustment.");

            if (countedQuantity < QuantityReserved)
                throw new DomainException(
                    $"{QuantityReserved} unit(s) are reserved for orders, so the counted quantity cannot be below that.");

            var change = countedQuantity - QuantityOnHand;
            if (change == 0)
                throw new DomainException("The counted quantity equals the current stock. Nothing to adjust.");

            QuantityOnHand = countedQuantity;
            Version = Guid.NewGuid();

            return StockMovement.Create(this, StockMovementType.Adjustment, change, reference: null, cleanReason);
        }

        /// <summary>
        /// Promises units to an order (it was confirmed). They stay physically here and the ledger does not
        /// change, because nothing moved: they simply stop being available to anyone else.
        /// </summary>
        public void Reserve(int quantity)
        {
            RequirePositive(quantity);

            if (quantity > QuantityAvailable)
                throw new DomainException(
                    $"Not enough available stock to reserve: {QuantityAvailable} available, {quantity} requested.");

            QuantityReserved += quantity;
            Version = Guid.NewGuid();
        }

        /// <summary>Gives promised units back (the order was cancelled): they become available again.</summary>
        public void Release(int quantity)
        {
            RequirePositive(quantity);

            if (quantity > QuantityReserved)
                throw new DomainException(
                    $"Cannot release more than is reserved: {QuantityReserved} reserved, {quantity} requested.");

            QuantityReserved -= quantity;
            Version = Guid.NewGuid();
        }

        /// <summary>
        /// Sends promised units out (the order was fulfilled): they leave the warehouse AND stop being reserved.
        /// Unlike a manual issue this one is allowed to use reserved stock, because that is what the reservation
        /// was for. Returns the ledger line to save together with this level.
        /// </summary>
        public StockMovement IssueReserved(int quantity, string? reference = null, string? notes = null)
        {
            RequirePositive(quantity);
            var cleanReference = StockMovement.CleanText(reference, "Reference", StockMovement.ReferenceMaxLength);
            var cleanNotes = StockMovement.CleanText(notes, "Notes", StockMovement.NotesMaxLength);

            if (quantity > QuantityReserved)
                throw new DomainException(
                    $"Cannot issue more than is reserved: {QuantityReserved} reserved, {quantity} requested.");

            QuantityReserved -= quantity;
            QuantityOnHand -= quantity;
            Version = Guid.NewGuid();

            return StockMovement.Create(this, StockMovementType.Issue, -quantity, cleanReference, cleanNotes);
        }

        private static void RequirePositive(int quantity)
        {
            if (quantity <= 0)
                throw new DomainException("Quantity must be greater than zero.");
        }
    }
}
