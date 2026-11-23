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
    /// How many units of ONE product are on hand in ONE warehouse. There is at most one level per product and
    /// warehouse (a unique index guarantees it). The quantity can only change through Receive, Issue and
    /// Adjust, and each of them returns the ledger line that records the change, so the number and its
    /// history can never drift apart. The Version token makes two people changing the same stock at the same
    /// moment safe: the database rejects the second save instead of silently losing one change.
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

        /// <summary>Whole units. Never negative (a check constraint in the database says so as well).</summary>
        public int QuantityOnHand { get; private set; }

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

        /// <summary>Stock went out. Refuses to go below zero. Returns the ledger line to save together with this level.</summary>
        public StockMovement Issue(int quantity, string? reference = null, string? notes = null)
        {
            RequirePositive(quantity);
            var cleanReference = StockMovement.CleanText(reference, "Reference", StockMovement.ReferenceMaxLength);
            var cleanNotes = StockMovement.CleanText(notes, "Notes", StockMovement.NotesMaxLength);

            if (quantity > QuantityOnHand)
                throw new DomainException($"Not enough stock: {QuantityOnHand} on hand, {quantity} requested.");

            QuantityOnHand -= quantity;
            Version = Guid.NewGuid();

            return StockMovement.Create(this, StockMovementType.Issue, -quantity, cleanReference, cleanNotes);
        }

        /// <summary>
        /// A stock count: sets the quantity to what was actually counted. The ledger records the difference,
        /// either sign, with the (required) reason. Counting the same number as the system has is refused,
        /// because there is nothing to correct and an empty ledger line would only be noise.
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

            var change = countedQuantity - QuantityOnHand;
            if (change == 0)
                throw new DomainException("The counted quantity equals the current stock. Nothing to adjust.");

            QuantityOnHand = countedQuantity;
            Version = Guid.NewGuid();

            return StockMovement.Create(this, StockMovementType.Adjustment, change, reference: null, cleanReason);
        }

        private static void RequirePositive(int quantity)
        {
            if (quantity <= 0)
                throw new DomainException("Quantity must be greater than zero.");
        }
    }
}
