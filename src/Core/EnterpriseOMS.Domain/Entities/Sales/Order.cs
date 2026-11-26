using EnterpriseOMS.Domain.Common;
using EnterpriseOMS.Domain.Common.Exceptions;
using EnterpriseOMS.Domain.Entities.Catalog;
using EnterpriseOMS.Domain.Entities.Inventory;
using EnterpriseOMS.Domain.Entities.Warehousing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.Entities.Sales
{
    /// <summary>A product and how many of it to put on an order. The order turns it into a line.</summary>
    public readonly record struct OrderLineInput(Product Product, int Quantity);

    /// <summary>
    /// A sales order: a customer buys products out of ONE warehouse. It starts as a DRAFT (freely editable, no
    /// effect on stock). CONFIRMING it reserves the stock, so two orders can never be promised the same units.
    /// FULFILLING it sends the reserved units out (ledger lines are written). CANCELLING it gives any reservation
    /// back. Every rule of that life is here, and every method validates everything BEFORE changing anything, so
    /// a refused call leaves the order and the stock exactly as they were.
    /// </summary>
    public class Order : BaseAuditableEntity, IAggregateRoot, ITenantEntity
    {
        public const int NotesMaxLength = 500;
        public const int ReasonMaxLength = 500;
        public const int MaxLines = 100;
        public const int MaxLineQuantity = 1_000_000;

        private static readonly Regex CurrencyPattern = new("^[A-Z]{3}$", RegexOptions.Compiled);

        private readonly List<OrderLine> _lines = new();

        // Required by EF Core. All other code must go through CreateDraft(), which enforces the rules.
        private Order() { }

        public int OrganizationId { get; private set; }

        /// <summary>1, 2, 3... within the organization. Shown as SO-000001.</summary>
        public int OrderNumber { get; private set; }

        public int CustomerId { get; private set; }

        /// <summary>The ONE warehouse this order is reserved in and shipped from.</summary>
        public int WarehouseId { get; private set; }

        public OrderStatus Status { get; private set; } = OrderStatus.Draft;

        /// <summary>Three upper-case letters, for example EUR or INR. Product prices are taken to be in this currency.</summary>
        public string CurrencyCode { get; private set; } = default!;

        public string? Notes { get; private set; }
        public DateTime? ConfirmedAt { get; private set; }
        public DateTime? FulfilledAt { get; private set; }
        public DateTime? CancelledAt { get; private set; }
        public string? CancellationReason { get; private set; }

        /// <summary>Optimistic-concurrency token: a new value on every change.</summary>
        public Guid Version { get; private set; } = Guid.NewGuid();

        public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();

        public string OrderNumberText => FormatNumber(OrderNumber);
        public decimal TotalAmount => _lines.Sum(line => line.LineTotal);
        public int TotalQuantity => _lines.Sum(line => line.Quantity);

        public static string FormatNumber(int orderNumber) => $"SO-{orderNumber:D6}";

        // ---------------- draft ----------------

        public static Order CreateDraft(
            int orderNumber,
            Customer customer,
            Warehouse warehouse,
            string currencyCode,
            string? notes,
            IEnumerable<OrderLineInput> lines)
        {
            if (orderNumber <= 0)
                throw new DomainException("An order needs a positive order number.");

            EnsureParties(customer, warehouse);

            var cleanCurrency = (currencyCode ?? string.Empty).Trim().ToUpperInvariant();
            if (!CurrencyPattern.IsMatch(cleanCurrency))
                throw new DomainException("Currency must be a 3-letter code such as EUR or INR.");

            var cleanNotes = CleanText(notes, "Notes", NotesMaxLength);
            var builtLines = BuildLines(customer.OrganizationId, lines);

            var order = new Order
            {
                OrganizationId = customer.OrganizationId,
                OrderNumber = orderNumber,
                CustomerId = customer.Id,
                WarehouseId = warehouse.Id,
                Status = OrderStatus.Draft,
                CurrencyCode = cleanCurrency,
                Notes = cleanNotes
            };

            order._lines.AddRange(builtLines);
            return order;
        }

        /// <summary>Replaces what a DRAFT says (customer, warehouse, notes and ALL its lines). The currency stays.</summary>
        public void UpdateDraft(Customer customer, Warehouse warehouse, string? notes, IEnumerable<OrderLineInput> lines)
        {
            if (Status != OrderStatus.Draft)
                throw new DomainException("Only a draft order can be changed.");

            EnsureParties(customer, warehouse);

            if (customer.OrganizationId != OrganizationId)
                throw new DomainException("The customer and the warehouse belong to a different organization than the order.");

            var cleanNotes = CleanText(notes, "Notes", NotesMaxLength);
            var builtLines = BuildLines(OrganizationId, lines);

            CustomerId = customer.Id;
            WarehouseId = warehouse.Id;
            Notes = cleanNotes;
            _lines.Clear();
            _lines.AddRange(builtLines);
            Version = Guid.NewGuid();
        }

        // ---------------- confirm: reserve the stock ----------------

        /// <summary>
        /// Confirms a draft: checks every line can be supplied from AVAILABLE stock and, if so, reserves all of
        /// it. If ANY line falls short nothing is reserved and the message lists every shortage at once, so the
        /// user can fix the whole order in one go. The caller passes the stock levels of this order's warehouse,
        /// by product id (a product with no level has nothing available).
        /// </summary>
        public void Confirm(
            Customer customer,
            Warehouse warehouse,
            IReadOnlyDictionary<int, StockLevel> levelsByProductId,
            DateTime now)
        {
            if (Status != OrderStatus.Draft)
                throw new DomainException("Only a draft order can be confirmed.");

            if (_lines.Count == 0)
                throw new DomainException("An order needs at least one line before it can be confirmed.");

            if (customer.Id != CustomerId || warehouse.Id != WarehouseId)
                throw new DomainException("The customer and the warehouse must be the ones on the order.");

            if (!customer.IsActive)
                throw new DomainException("The customer is inactive, so this order cannot be confirmed.");

            if (!warehouse.IsActive)
                throw new DomainException("The warehouse is inactive, so this order cannot be confirmed.");

            var shortages = new List<string>();

            foreach (var line in _lines)
            {
                var available = 0;

                if (levelsByProductId.TryGetValue(line.ProductId, out var level))
                {
                    EnsureLevelBelongsHere(level);
                    available = level.QuantityAvailable;
                }

                if (available < line.Quantity)
                    shortages.Add($"{line.ProductSku} ({available} available, {line.Quantity} needed)");
            }

            if (shortages.Count > 0)
                throw new DomainException($"Not enough available stock to confirm this order: {string.Join("; ", shortages)}.");

            // Everything checked: reserving cannot fail now.
            foreach (var line in _lines)
            {
                levelsByProductId[line.ProductId].Reserve(line.Quantity);
                line.ReserveAll();
            }

            Status = OrderStatus.Confirmed;
            ConfirmedAt = now;
            Version = Guid.NewGuid();
        }

        // ---------------- cancel: give the reservation back ----------------

        /// <summary>
        /// Cancels a draft (nothing to undo) or a confirmed order (every reservation is released). A fulfilled
        /// order cannot be cancelled: its stock has left, and taking it back is a return, which is a later module.
        /// </summary>
        public void Cancel(IReadOnlyDictionary<int, StockLevel> levelsByProductId, string? reason, DateTime now)
        {
            if (Status == OrderStatus.Cancelled)
                throw new DomainException("This order is already cancelled.");

            if (Status == OrderStatus.Fulfilled)
                throw new DomainException("A fulfilled order cannot be cancelled: its stock has already left the warehouse.");

            var cleanReason = CleanText(reason, "Reason", ReasonMaxLength);

            if (Status == OrderStatus.Confirmed)
            {
                // Check every level first, so a problem leaves the order and ALL the stock untouched.
                foreach (var line in _lines)
                {
                    var level = RequireLevel(levelsByProductId, line);

                    if (level.QuantityReserved < line.ReservedQuantity)
                        throw new DomainException(
                            $"The stock reserved for {line.ProductSku} is less than this order holds. Please contact support.");
                }

                foreach (var line in _lines)
                {
                    levelsByProductId[line.ProductId].Release(line.ReservedQuantity);
                    line.ClearReservation();
                }
            }

            Status = OrderStatus.Cancelled;
            CancelledAt = now;
            CancellationReason = cleanReason;
            Version = Guid.NewGuid();
        }

        // ---------------- fulfil: the stock leaves ----------------

        /// <summary>
        /// Fulfils a confirmed order: every reserved unit leaves the warehouse and the ledger records it. Returns
        /// one ledger line per order line, to be saved together with the stock levels and this order.
        /// </summary>
        public IReadOnlyList<StockMovement> Fulfil(IReadOnlyDictionary<int, StockLevel> levelsByProductId, DateTime now)
        {
            if (Status != OrderStatus.Confirmed)
                throw new DomainException("Only a confirmed order can be fulfilled.");

            foreach (var line in _lines)
            {
                var level = RequireLevel(levelsByProductId, line);

                if (level.QuantityReserved < line.ReservedQuantity)
                    throw new DomainException(
                        $"The stock reserved for {line.ProductSku} is less than this order holds. Please contact support.");
            }

            var movements = new List<StockMovement>();

            foreach (var line in _lines)
            {
                movements.Add(
                    levelsByProductId[line.ProductId].IssueReserved(line.ReservedQuantity, OrderNumberText, $"Fulfilment of order {OrderNumberText}"));
                line.MarkFulfilled();
            }

            Status = OrderStatus.Fulfilled;
            FulfilledAt = now;
            Version = Guid.NewGuid();

            return movements;
        }

        // ---------------- helpers ----------------

        private static void EnsureParties(Customer customer, Warehouse warehouse)
        {
            if (customer.Id <= 0 || warehouse.Id <= 0)
                throw new DomainException("The customer and the warehouse must be saved before an order can be made.");

            if (customer.OrganizationId != warehouse.OrganizationId)
                throw new DomainException("The customer and the warehouse belong to different organizations.");

            if (!customer.IsActive)
                throw new DomainException("An order needs an active customer.");

            if (!warehouse.IsActive)
                throw new DomainException("An order needs an active warehouse.");
        }

        /// <summary>
        /// Turns the requested products into lines: one line per product (repeats are added together), only active
        /// products of this organization, sensible quantities, and a sensible number of lines.
        /// </summary>
        private static List<OrderLine> BuildLines(int organizationId, IEnumerable<OrderLineInput> inputs)
        {
            var merged = new List<(Product Product, long Quantity)>();

            foreach (var input in inputs ?? Enumerable.Empty<OrderLineInput>())
            {
                var product = input.Product;

                if (product.Id <= 0)
                    throw new DomainException("Every product must be saved before it can be put on an order.");

                if (product.OrganizationId != organizationId)
                    throw new DomainException($"Product {product.Sku} belongs to a different organization.");

                if (!product.IsActive)
                    throw new DomainException($"Product {product.Sku} is inactive and cannot be ordered.");

                if (input.Quantity <= 0)
                    throw new DomainException("Quantity must be greater than zero.");

                var existing = merged.FindIndex(m => m.Product.Id == product.Id);

                if (existing >= 0)
                    merged[existing] = (product, merged[existing].Quantity + input.Quantity);
                else
                    merged.Add((product, input.Quantity));
            }

            foreach (var (product, quantity) in merged)
            {
                if (quantity > MaxLineQuantity)
                    throw new DomainException($"The quantity of {product.Sku} cannot exceed 1,000,000 units on one order line.");
            }

            if (merged.Count > MaxLines)
                throw new DomainException($"An order can have at most {MaxLines} lines.");

            return merged.Select(m => OrderLine.Create(organizationId, m.Product, (int)m.Quantity)).ToList();
        }

        private void EnsureLevelBelongsHere(StockLevel level)
        {
            if (level.WarehouseId != WarehouseId)
                throw new DomainException("A stock level from a different warehouse was supplied for this order.");
        }

        private StockLevel RequireLevel(IReadOnlyDictionary<int, StockLevel> levels, OrderLine line)
        {
            if (!levels.TryGetValue(line.ProductId, out var level))
                throw new DomainException($"There is no stock record for {line.ProductSku} in the order's warehouse.");

            EnsureLevelBelongsHere(level);
            return level;
        }

        private static string? CleanText(string? value, string label, int maxLength)
        {
            var trimmed = value?.Trim();

            if (string.IsNullOrEmpty(trimmed)) return null;

            if (trimmed.Length > maxLength)
                throw new DomainException($"{label} must be {maxLength} characters or fewer.");

            return trimmed;
        }
    }
}
