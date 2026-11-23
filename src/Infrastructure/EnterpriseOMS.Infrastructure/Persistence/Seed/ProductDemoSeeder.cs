using EnterpriseOMS.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnterpriseOMS.Infrastructure.Persistence.Seed;

/// <summary>
/// Gives the seeded "Default Organization" a realistic sample catalog so the product screens have
/// something to show. Idempotent: it does nothing if that organization already has any product.
/// </summary>
public class ProductDemoSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly SeedDemoDataSettings _settings;
    private readonly ILogger<ProductDemoSeeder> _logger;

    public ProductDemoSeeder(
        ApplicationDbContext context,
        IOptions<SeedDemoDataSettings> options,
        ILogger<ProductDemoSeeder> logger)
    {
        _context = context;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_settings.Products) return;

        var organization = await _context.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Code == IdentitySeeder.DefaultOrganizationCode, cancellationToken);

        if (organization is null) return;

        var alreadySeeded = await _context.Products
            .IgnoreQueryFilters()
            .AnyAsync(p => p.OrganizationId == organization.Id, cancellationToken);

        if (alreadySeeded) return;

        var products = BuildSamples(organization.Id);

        _context.Products.AddRange(products);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Seeded {Count} demo products.", products.Count);
    }

    private static List<Product> BuildSamples(int organizationId)
    {
        Product Make(string sku, string name, string category, UnitOfMeasure unit, decimal price, decimal cost, int reorder) =>
            Product.Create(organizationId, sku, name, null, category, unit, price, cost, reorder);

        var discontinued = Make("DISC-001", "Discontinued Desk Lamp", "Furniture", UnitOfMeasure.Piece, 29.00m, 15.00m, 0);
        discontinued.Deactivate();   // one inactive product so the "inactive" filter has something to show

        return new List<Product>
        {
            Make("LAP-001", "Business Laptop 14\"", "Electronics", UnitOfMeasure.Piece, 899.00m, 690.00m, 5),
            Make("MON-024", "24-inch Monitor", "Electronics", UnitOfMeasure.Piece, 179.99m, 120.00m, 8),
            Make("KBD-010", "Wireless Keyboard", "Accessories", UnitOfMeasure.Piece, 39.90m, 22.50m, 20),
            Make("MSE-011", "Wireless Mouse", "Accessories", UnitOfMeasure.Piece, 24.50m, 12.00m, 25),
            Make("CAB-USB-C2", "USB-C Cable 2 m", "Accessories", UnitOfMeasure.Piece, 9.99m, 3.20m, 50),
            Make("PAP-A4-500", "A4 Copy Paper (500 sheets)", "Stationery", UnitOfMeasure.Pack, 6.75m, 4.10m, 100),
            Make("PEN-BLU-12", "Ballpoint Pens, Blue (12)", "Stationery", UnitOfMeasure.Box, 4.20m, 2.30m, 60),
            Make("TON-HP-85A", "Toner Cartridge 85A", "Consumables", UnitOfMeasure.Piece, 64.00m, 41.00m, 6),
            Make("WTR-5L", "Spring Water 5 L", "Beverages", UnitOfMeasure.Piece, 2.10m, 1.05m, 200),
            Make("COF-BEAN-1K", "Coffee Beans 1 kg", "Beverages", UnitOfMeasure.Kilogram, 18.50m, 11.00m, 30),
            Make("PLT-EUR", "Euro Pallet", "Packaging", UnitOfMeasure.Pallet, 14.00m, 8.50m, 40),
            Make("BOX-M-50", "Shipping Box, Medium (50)", "Packaging", UnitOfMeasure.Pack, 32.00m, 19.00m, 15),
            discontinued
        };
    }
}