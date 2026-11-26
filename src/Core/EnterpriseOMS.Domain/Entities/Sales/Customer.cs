using EnterpriseOMS.Domain.Common;
using EnterpriseOMS.Domain.Common.Exceptions;
using EnterpriseOMS.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.Entities.Sales
{
    /// <summary>
    /// Someone the organization sells to. Tenant-owned, identified by a short code that is unique within the
    /// organization and never changes (like a warehouse code), and protected against lost updates by a Version
    /// token. Customers are deactivated, never deleted: orders keep pointing at them.
    /// </summary>
    public class Customer : BaseAuditableEntity, IAggregateRoot, ITenantEntity
    {
        public const int CodeMaxLength = 30;
        public const int NameMaxLength = 200;
        public const int EmailMaxLength = 200;
        public const int PhoneMaxLength = 30;

        private static readonly Regex CodePattern = new(@"^[A-Za-z0-9][A-Za-z0-9._/\-]*$", RegexOptions.Compiled);
        private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
        private static readonly Regex PhonePattern = new(@"^[0-9+()\-.\s]{5,30}$", RegexOptions.Compiled);

        // Required by EF Core. All other code must go through Create(), which enforces the rules.
        private Customer() { }

        public int OrganizationId { get; private set; }

        /// <summary>The code as typed (for example "ACME-01"). It never changes after creation.</summary>
        public string Code { get; private set; } = default!;

        /// <summary>Upper-case code, used for uniqueness so "acme-01" and "ACME-01" cannot both exist.</summary>
        public string NormalizedCode { get; private set; } = default!;

        public string Name { get; private set; } = default!;
        public string? Email { get; private set; }
        public string? Phone { get; private set; }
        public Address BillingAddress { get; private set; } = default!;
        public bool IsActive { get; private set; } = true;

        /// <summary>Optimistic-concurrency token: a new value on every change (see Product.Version).</summary>
        public Guid Version { get; private set; } = Guid.NewGuid();

        public static string NormalizeCode(string code) => (code ?? string.Empty).Trim().ToUpperInvariant();

        public static Customer Create(int organizationId, string code, string name, string? email, string? phone, Address billingAddress)
        {
            if (organizationId <= 0)
                throw new DomainException("A customer must belong to an organization.");

            var cleanCode = (code ?? string.Empty).Trim();
            if (cleanCode.Length == 0)
                throw new DomainException("Customer code is required.");
            if (cleanCode.Length > CodeMaxLength)
                throw new DomainException($"Customer code must be {CodeMaxLength} characters or fewer.");
            if (!CodePattern.IsMatch(cleanCode))
                throw new DomainException(
                    "Customer code may contain only letters, digits, dots, hyphens, underscores and slashes, and must start with a letter or digit.");

            var customer = new Customer
            {
                OrganizationId = organizationId,
                Code = cleanCode,
                NormalizedCode = NormalizeCode(cleanCode),
                IsActive = true
            };

            customer.ApplyDetails(name, email, phone, billingAddress);
            return customer;
        }

        /// <summary>Changes the details (never the code). Validates first, then assigns.</summary>
        public void Update(string name, string? email, string? phone, Address billingAddress) =>
            ApplyDetails(name, email, phone, billingAddress);

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

        private void ApplyDetails(string name, string? email, string? phone, Address billingAddress)
        {
            var cleanName = (name ?? string.Empty).Trim();
            if (cleanName.Length == 0)
                throw new DomainException("Customer name is required.");
            if (cleanName.Length > NameMaxLength)
                throw new DomainException($"Customer name must be {NameMaxLength} characters or fewer.");

            var cleanEmail = email?.Trim();
            if (string.IsNullOrEmpty(cleanEmail))
            {
                cleanEmail = null;
            }
            else if (cleanEmail.Length > EmailMaxLength)
            {
                throw new DomainException($"Email must be {EmailMaxLength} characters or fewer.");
            }
            else if (!EmailPattern.IsMatch(cleanEmail))
            {
                throw new DomainException("Email is not a valid email address.");
            }

            var cleanPhone = phone?.Trim();
            if (string.IsNullOrEmpty(cleanPhone))
            {
                cleanPhone = null;
            }
            else if (!PhonePattern.IsMatch(cleanPhone))
            {
                throw new DomainException(
                    $"Phone may contain only digits, spaces and + ( ) - . characters, and be 5 to {PhoneMaxLength} characters long.");
            }

            if (billingAddress is null)
                throw new DomainException("A customer needs a billing address.");

            Name = cleanName;
            Email = cleanEmail;
            Phone = cleanPhone;
            BillingAddress = billingAddress;
            Version = Guid.NewGuid();
        }
    }
}
