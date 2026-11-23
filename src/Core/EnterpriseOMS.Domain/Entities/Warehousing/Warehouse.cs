using System.Text.RegularExpressions;
using EnterpriseOMS.Domain.Common;
using EnterpriseOMS.Domain.Common.Exceptions;
using EnterpriseOMS.Domain.ValueObjects;

namespace EnterpriseOMS.Domain.Entities.Warehousing;

/// <summary>
/// A physical place where an organization keeps stock. Tenant-owned (ITenantEntity), identified by a
/// short code that is unique within the organization and never changes, and protected against lost
/// updates by the same Version token as Product.
/// </summary>
public class Warehouse : BaseAuditableEntity, IAggregateRoot, ITenantEntity
{
    public const int CodeMaxLength = 30;
    public const int NameMaxLength = 200;

    private static readonly Regex CodePattern = new(@"^[A-Za-z0-9][A-Za-z0-9._/\-]*$", RegexOptions.Compiled);

    // Required by EF Core. All other code must go through Create(), which enforces the rules.
    private Warehouse() { }

    public int OrganizationId { get; private set; }

    /// <summary>The code as typed (for example "WH-Main"). It never changes after creation.</summary>
    public string Code { get; private set; } = default!;

    /// <summary>Upper-case code, used for uniqueness so "wh-main" and "WH-MAIN" cannot both exist.</summary>
    public string NormalizedCode { get; private set; } = default!;

    public string Name { get; private set; } = default!;
    public Address Address { get; private set; } = default!;

    /// <summary>Warehouses are deactivated, not deleted: stock and orders will reference them.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Optimistic-concurrency token: a new value on every change (see Product.Version).</summary>
    public Guid Version { get; private set; } = Guid.NewGuid();

    public static string NormalizeCode(string code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    public static Warehouse Create(int organizationId, string code, string name, Address address)
    {
        if (organizationId <= 0)
            throw new DomainException("A warehouse must belong to an organization.");

        var cleanCode = (code ?? string.Empty).Trim();
        if (cleanCode.Length == 0)
            throw new DomainException("Warehouse code is required.");
        if (cleanCode.Length > CodeMaxLength)
            throw new DomainException($"Warehouse code must be {CodeMaxLength} characters or fewer.");
        if (!CodePattern.IsMatch(cleanCode))
            throw new DomainException(
                "Warehouse code may contain only letters, digits, dots, hyphens, underscores and slashes, and must start with a letter or digit.");

        var warehouse = new Warehouse
        {
            OrganizationId = organizationId,
            Code = cleanCode,
            NormalizedCode = NormalizeCode(cleanCode),
            IsActive = true
        };

        warehouse.ApplyDetails(name, address);
        return warehouse;
    }

    /// <summary>Changes the name and address (never the code). Validates first, then assigns.</summary>
    public void Update(string name, Address address) => ApplyDetails(name, address);

    /// <summary>
    /// A warehouse that still holds stock cannot be deactivated: the stock would be stranded in a place
    /// nobody can receive into or ship from. The caller (which can query the stock) passes how many
    /// products are still on hand here; the rule itself lives in the domain.
    /// </summary>
    public void Deactivate(int stockedProductCount = 0)
    {
        if (!IsActive) return;

        if (stockedProductCount > 0)
            throw new DomainException(
                $"This warehouse still holds stock of {stockedProductCount} product(s). Issue or move it out first.");

        IsActive = false;
        Version = Guid.NewGuid();
    }

    public void Activate()
    {
        if (IsActive) return;

        IsActive = true;
        Version = Guid.NewGuid();
    }

    private void ApplyDetails(string name, Address address)
    {
        var cleanName = (name ?? string.Empty).Trim();
        if (cleanName.Length == 0)
            throw new DomainException("Warehouse name is required.");
        if (cleanName.Length > NameMaxLength)
            throw new DomainException($"Warehouse name must be {NameMaxLength} characters or fewer.");

        if (address is null)
            throw new DomainException("A warehouse needs an address.");

        Name = cleanName;
        Address = address;
        Version = Guid.NewGuid();
    }
}