using EnterpriseOMS.Domain.Common;
using EnterpriseOMS.Domain.Common.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.Entities.Inventory
{
    /// <summary>
    /// One line of the stock LEDGER: "on this date, this user changed this product's stock in this warehouse
    /// by this much, leaving this much". Ledger lines are APPEND-ONLY: they have no public setters, only the
    /// stock level creates them (as part of changing the quantity), and the database context refuses to
    /// update or delete one. Created and CreatedBy (stamped on save) say when and by whom.
    /// </summary>
    public class StockMovement : BaseAuditableEntity, ITenantEntity
    {
        public const int ReferenceMaxLength = 100;
        public const int NotesMaxLength = 500;

        // Required by EF Core. Movements are only ever created by StockLevel.
        private StockMovement() { }

        public int OrganizationId { get; private set; }
        public int ProductId { get; private set; }
        public int WarehouseId { get; private set; }
        public StockMovementType Type { get; private set; }

        /// <summary>Signed: positive for stock in, negative for stock out.</summary>
        public int QuantityChange { get; private set; }

        /// <summary>What the stock level was right after this movement: a running balance you can read down the ledger.</summary>
        public int QuantityAfter { get; private set; }

        /// <summary>An outside document number, such as a purchase order or delivery note. Optional.</summary>
        public string? Reference { get; private set; }

        /// <summary>Free text. For an adjustment this holds the (required) reason.</summary>
        public string? Notes { get; private set; }

        /// <summary>Only a stock level may record a movement, so the ledger can never disagree with the quantities.</summary>
        internal static StockMovement Create(
            StockLevel level,
            StockMovementType type,
            int quantityChange,
            string? reference,
            string? notes) =>
            new()
            {
                OrganizationId = level.OrganizationId,
                ProductId = level.ProductId,
                WarehouseId = level.WarehouseId,
                Type = type,
                QuantityChange = quantityChange,
                QuantityAfter = level.QuantityOnHand,
                Reference = reference,
                Notes = notes
            };

        /// <summary>Trims the text, turns blank into null, and refuses text that is too long.</summary>
        internal static string? CleanText(string? value, string label, int maxLength)
        {
            var trimmed = value?.Trim();

            if (string.IsNullOrEmpty(trimmed))
                return null;

            if (trimmed.Length > maxLength)
                throw new DomainException($"{label} must be {maxLength} characters or fewer.");

            return trimmed;
        }
    }
}
