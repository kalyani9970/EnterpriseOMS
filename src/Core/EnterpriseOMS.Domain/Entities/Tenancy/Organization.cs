using EnterpriseOMS.Domain.Common;
using EnterpriseOMS.Domain.Common.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.Entities.Tenancy
{
    /// <summary>
    /// A tenant: one customer company using the platform. Everything else in the system
    /// (users, products, warehouses, orders...) belongs to exactly one organization.
    /// Organization itself is NOT an ITenantEntity: it is the root the others point to.
    /// </summary>
    public class Organization : BaseAuditableEntity, IAggregateRoot
    {
        public const int NameMaxLength = 200;
        public const int CodeMaxLength = 50;

        private static readonly Regex CodePattern = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);

        // Required by EF Core. All other code must go through Create(), which enforces the rules.
        private Organization() { }

        public string Name { get; private set; } = default!;

        /// <summary>Short, URL-safe, unique identifier, e.g. "acme-traders". Never changes after creation.</summary>
        public string Code { get; private set; } = default!;

        public bool IsActive { get; private set; } = true;

        public static Organization Create(string name, string code)
        {
            var normalizedCode = code?.Trim() ?? string.Empty;

            if (normalizedCode.Length == 0 || normalizedCode.Length > CodeMaxLength || !CodePattern.IsMatch(normalizedCode))
            {
                throw new DomainException(
                    "Organization code must be lowercase letters, digits and single hyphens, up to 50 characters.");
            }

            return new Organization
            {
                Name = NormalizeName(name),
                Code = normalizedCode,
                IsActive = true
            };
        }

        public void Rename(string name) => Name = NormalizeName(name);

        /// <summary>A deactivated organization's users can no longer sign in or refresh tokens.</summary>
        public void Deactivate() => IsActive = false;

        public void Activate() => IsActive = true;

        private static string NormalizeName(string? name)
        {
            var trimmed = name?.Trim() ?? string.Empty;

            if (trimmed.Length == 0)
                throw new DomainException("Organization name is required.");

            if (trimmed.Length > NameMaxLength)
                throw new DomainException($"Organization name must be {NameMaxLength} characters or fewer.");

            return trimmed;
        }
    }
}
