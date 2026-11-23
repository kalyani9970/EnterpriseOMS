using EnterpriseOMS.Domain.Common;
using EnterpriseOMS.Domain.Common.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.Entities.Catalog
{
    /// <summary>
    /// An item an organization buys, stores and sells. The first tenant-owned business entity:
    /// it implements ITenantEntity, so the persistence layer filters it by organization automatically.
    /// Every rule about what a valid product is lives HERE, so no handler or controller can create an
    /// invalid one.
    /// </summary>
    public class Product : BaseAuditableEntity, IAggregateRoot, ITenantEntity
    {
        public const int SkuMaxLength = 50;
        public const int NameMaxLength = 200;
        public const int DescriptionMaxLength = 2000;
        public const int CategoryMaxLength = 100;
        public const decimal MaxPrice = 999_999_999.99m;
        public const int MaxReorderLevel = 1_000_000;

        private static readonly Regex SkuPattern = new(@"^[A-Za-z0-9][A-Za-z0-9._/\-]*$", RegexOptions.Compiled);

        // Required by EF Core. All other code must go through Create(), which enforces the rules.
        private Product() { }

        public int OrganizationId { get; private set; }

        /// <summary>The SKU as the user typed it. It never changes after creation: it is the product's business identifier.</summary>
        public string Sku { get; private set; } = default!;

        /// <summary>Upper-case SKU, used for uniqueness so "lap-001" and "LAP-001" cannot both exist.</summary>
        public string NormalizedSku { get; private set; } = default!;

        public string Name { get; private set; } = default!;
        public string? Description { get; private set; }
        public string? Category { get; private set; }
        public UnitOfMeasure UnitOfMeasure { get; private set; }
        public decimal UnitPrice { get; private set; }
        public decimal CostPrice { get; private set; }

        /// <summary>Stock level at or below which the product should be re-ordered (used by procurement and AI later).</summary>
        public int ReorderLevel { get; private set; }

        /// <summary>Products are deactivated, not deleted: orders and stock history will reference them.</summary>
        public bool IsActive { get; private set; } = true;

        /// <summary>
        /// Optimistic-concurrency token. It gets a new value on EVERY change. If two people edit the same
        /// product, the second save finds the token already changed and is rejected instead of silently
        /// overwriting the first person's work.
        /// </summary>
        public Guid Version { get; private set; } = Guid.NewGuid();

        public static string NormalizeSku(string sku) => (sku ?? string.Empty).Trim().ToUpperInvariant();

        public static Product Create(
            int organizationId,
            string sku,
            string name,
            string? description,
            string? category,
            UnitOfMeasure unitOfMeasure,
            decimal unitPrice,
            decimal costPrice,
            int reorderLevel)
        {
            if (organizationId <= 0)
                throw new DomainException("A product must belong to an organization.");

            var cleanSku = (sku ?? string.Empty).Trim();
            if (cleanSku.Length == 0)
                throw new DomainException("SKU is required.");
            if (cleanSku.Length > SkuMaxLength)
                throw new DomainException($"SKU must be {SkuMaxLength} characters or fewer.");
            if (!SkuPattern.IsMatch(cleanSku))
                throw new DomainException(
                    "SKU may contain only letters, digits, dots, hyphens, underscores and slashes, and must start with a letter or digit.");

            var product = new Product
            {
                OrganizationId = organizationId,
                Sku = cleanSku,
                NormalizedSku = NormalizeSku(cleanSku),
                IsActive = true
            };

            product.ApplyDetails(name, description, category, unitOfMeasure, unitPrice, costPrice, reorderLevel);
            return product;
        }

        /// <summary>Changes everything except the SKU and the organization. Validates first, then assigns, so a failed update changes nothing.</summary>
        public void Update(
            string name,
            string? description,
            string? category,
            UnitOfMeasure unitOfMeasure,
            decimal unitPrice,
            decimal costPrice,
            int reorderLevel)
        {
            ApplyDetails(name, description, category, unitOfMeasure, unitPrice, costPrice, reorderLevel);
        }

        /// <summary>Idempotent: deactivating an inactive product changes nothing (and keeps its version).</summary>
        public void Deactivate()
        {
            if (!IsActive) return;

            IsActive = false;
            Version = Guid.NewGuid();
        }

        public void Activate()
        {
            if (IsActive) return;

            IsActive = true;
            Version = Guid.NewGuid();
        }

        private void ApplyDetails(
            string name,
            string? description,
            string? category,
            UnitOfMeasure unitOfMeasure,
            decimal unitPrice,
            decimal costPrice,
            int reorderLevel)
        {
            // 1. Validate everything...
            var cleanName = (name ?? string.Empty).Trim();
            if (cleanName.Length == 0)
                throw new DomainException("Product name is required.");
            if (cleanName.Length > NameMaxLength)
                throw new DomainException($"Product name must be {NameMaxLength} characters or fewer.");

            var cleanDescription = EmptyToNull(description);
            if (cleanDescription is { Length: > DescriptionMaxLength })
                throw new DomainException($"Description must be {DescriptionMaxLength} characters or fewer.");

            var cleanCategory = EmptyToNull(category);
            if (cleanCategory is { Length: > CategoryMaxLength })
                throw new DomainException($"Category must be {CategoryMaxLength} characters or fewer.");

            if (!Enum.IsDefined(unitOfMeasure))
                throw new DomainException("Unit of measure is not valid.");

            EnsureValidPrice(unitPrice, "Unit price");
            EnsureValidPrice(costPrice, "Cost price");

            if (reorderLevel < 0 || reorderLevel > MaxReorderLevel)
                throw new DomainException($"Reorder level must be between 0 and {MaxReorderLevel:N0}.");

            // 2. ...then assign, so a rejected change leaves the product exactly as it was.
            Name = cleanName;
            Description = cleanDescription;
            Category = cleanCategory;
            UnitOfMeasure = unitOfMeasure;
            UnitPrice = unitPrice;
            CostPrice = costPrice;
            ReorderLevel = reorderLevel;
            Version = Guid.NewGuid();
        }

        private static void EnsureValidPrice(decimal price, string label)
        {
            if (price < 0 || price > MaxPrice || decimal.Round(price, 2) != price)
                throw new DomainException($"{label} must be between 0 and 999,999,999.99 with at most 2 decimal places.");
        }

        private static string? EmptyToNull(string? value)
        {
            var trimmed = value?.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }
    }
}
