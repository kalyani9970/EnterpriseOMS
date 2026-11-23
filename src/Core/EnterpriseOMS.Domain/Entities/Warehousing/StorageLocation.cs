using EnterpriseOMS.Domain.Common;
using EnterpriseOMS.Domain.Common.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.Entities.Warehousing
{
    /// <summary>
    /// A place inside a warehouse: a zone, aisle, rack, shelf or bin. Locations form a tree through
    /// ParentId. The domain enforces the shape of that tree so no caller can build a broken one:
    /// a child lives in the same warehouse as its parent, and always at a LOWER level (Zone &gt; Aisle &gt;
    /// Rack &gt; Shelf &gt; Bin), which makes cycles impossible by construction.
    /// </summary>
    public class StorageLocation : BaseAuditableEntity, IAggregateRoot, ITenantEntity
    {
        public const int CodeMaxLength = 30;
        public const int NameMaxLength = 200;

        private static readonly Regex CodePattern = new(@"^[A-Za-z0-9][A-Za-z0-9._/\-]*$", RegexOptions.Compiled);

        // Required by EF Core. All other code must go through Create(), which enforces the rules.
        private StorageLocation() { }

        public int OrganizationId { get; private set; }
        public int WarehouseId { get; private set; }

        /// <summary>The location this one sits inside, or null for a top-level location.</summary>
        public int? ParentId { get; private set; }

        /// <summary>Unique within its warehouse. Never changes after creation.</summary>
        public string Code { get; private set; } = default!;
        public string NormalizedCode { get; private set; } = default!;

        public string? Name { get; private set; }
        public StorageLocationType Type { get; private set; }
        public bool IsActive { get; private set; } = true;
        public Guid Version { get; private set; } = Guid.NewGuid();

        public static string NormalizeCode(string code) => (code ?? string.Empty).Trim().ToUpperInvariant();

        public static StorageLocation Create(
            Warehouse warehouse,
            StorageLocation? parent,
            string code,
            string? name,
            StorageLocationType type)
        {
            if (!warehouse.IsActive)
                throw new DomainException("Locations cannot be added to an inactive warehouse.");

            var cleanCode = (code ?? string.Empty).Trim();
            if (cleanCode.Length == 0)
                throw new DomainException("Location code is required.");
            if (cleanCode.Length > CodeMaxLength)
                throw new DomainException($"Location code must be {CodeMaxLength} characters or fewer.");
            if (!CodePattern.IsMatch(cleanCode))
                throw new DomainException(
                    "Location code may contain only letters, digits, dots, hyphens, underscores and slashes, and must start with a letter or digit.");

            if (!Enum.IsDefined(type))
                throw new DomainException("Location type is not valid.");

            if (parent is not null)
            {
                if (parent.WarehouseId != warehouse.Id)
                    throw new DomainException("The parent location belongs to a different warehouse.");

                if (!parent.IsActive)
                    throw new DomainException("The parent location is inactive. Reactivate it first.");

                if (type <= parent.Type)
                    throw new DomainException(
                        $"A {type} cannot be placed inside a {parent.Type}. Each level must be below its parent (Zone, Aisle, Rack, Shelf, Bin).");
            }

            return new StorageLocation
            {
                OrganizationId = warehouse.OrganizationId,
                WarehouseId = warehouse.Id,
                ParentId = parent?.Id,
                Code = cleanCode,
                NormalizedCode = NormalizeCode(cleanCode),
                Name = CleanName(name),
                Type = type,
                IsActive = true
            };
        }

        public void Rename(string? name)
        {
            Name = CleanName(name);
            Version = Guid.NewGuid();
        }

        /// <summary>A location that still contains active sub-locations cannot be deactivated: empty it out first.</summary>
        public void Deactivate(int activeChildCount)
        {
            if (!IsActive) return;

            if (activeChildCount > 0)
                throw new DomainException($"Deactivate its {activeChildCount} active sub-location(s) first.");

            IsActive = false;
            Version = Guid.NewGuid();
        }

        /// <summary>A location can only come back if the one it sits inside is active.</summary>
        public void Activate(StorageLocation? parent)
        {
            if (IsActive) return;

            if (parent is not null && !parent.IsActive)
                throw new DomainException("The parent location is inactive. Reactivate it first.");

            IsActive = true;
            Version = Guid.NewGuid();
        }

        private static string? CleanName(string? name)
        {
            var trimmed = name?.Trim();

            if (string.IsNullOrEmpty(trimmed))
                return null;

            if (trimmed.Length > NameMaxLength)
                throw new DomainException($"Location name must be {NameMaxLength} characters or fewer.");

            return trimmed;
        }
    }
}
