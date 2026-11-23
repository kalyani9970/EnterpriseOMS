using EnterpriseOMS.Domain.Entities.Catalog;
using EnterpriseOMS.Domain.Entities.Inventory;
using EnterpriseOMS.Domain.Entities.Warehousing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Infrastructure.Persistence.Seed
{
    /// <summary>
    /// Gives the demo warehouses opening stock of the demo products, with a little history, so the stock
    /// screens have something to show: 17 stock levels (12 in Main, 5 in East), 7 of them running low, and 19
    /// ledger lines of all three kinds. It goes through the real domain methods, so the seeded ledger obeys
    /// every rule a user's would. Idempotent: it does nothing once the organization has any movement. It needs
    /// the demo products and warehouses (seeded before it); missing ones are simply skipped.
    /// </summary>
    public class InventoryDemoSeeder
    {
        private const string MainCode = "WH-MAIN";
        private const string EastCode = "WH-EAST";

        private static readonly (string Sku, int Quantity)[] MainOpeningStock =
        {
        ("LAP-001", 14),        // 2 are issued below, leaving 12 (reorder level 5)
        ("MON-024", 8),         // counted down to 6 below: low (reorder level 8)
        ("KBD-010", 45),
        ("MSE-011", 25),        // exactly at its reorder level: low
        ("CAB-USB-C2", 120),
        ("PAP-A4-500", 250),
        ("PEN-BLU-12", 80),
        ("TON-HP-85A", 3),      // low (reorder level 6)
        ("WTR-5L", 400),
        ("COF-BEAN-1K", 40),
        ("PLT-EUR", 60),
        ("BOX-M-50", 30)
    };

        private static readonly (string Sku, int Quantity)[] EastOpeningStock =
        {
        ("LAP-001", 4),         // low
        ("KBD-010", 15),        // low
        ("CAB-USB-C2", 60),
        ("WTR-5L", 150),        // low
        ("PLT-EUR", 20)         // low
    };

        private readonly ApplicationDbContext _context;
        private readonly SeedDemoDataSettings _settings;
        private readonly ILogger<InventoryDemoSeeder> _logger;

        public InventoryDemoSeeder(
            ApplicationDbContext context,
            IOptions<SeedDemoDataSettings> options,
            ILogger<InventoryDemoSeeder> logger)
        {
            _context = context;
            _settings = options.Value;
            _logger = logger;
        }

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            if (!_settings.Inventory) return;

            var organization = await _context.Organizations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Code == IdentitySeeder.DefaultOrganizationCode, cancellationToken);

            if (organization is null) return;

            var alreadySeeded = await _context.StockMovements
                .IgnoreQueryFilters()
                .AnyAsync(m => m.OrganizationId == organization.Id, cancellationToken);

            if (alreadySeeded) return;

            var products = await _context.Products
                .IgnoreQueryFilters()
                .Where(p => p.OrganizationId == organization.Id)
                .ToDictionaryAsync(p => p.Sku, cancellationToken);

            var warehouses = await _context.Warehouses
                .IgnoreQueryFilters()
                .Where(w => w.OrganizationId == organization.Id)
                .ToDictionaryAsync(w => w.Code, cancellationToken);

            if (!warehouses.TryGetValue(MainCode, out var main) || !warehouses.TryGetValue(EastCode, out var east))
            {
                return;
            }

            var levels = new Dictionary<(int ProductId, int WarehouseId), StockLevel>();
            var movements = new List<StockMovement>();

            StockLevel LevelFor(Product product, Warehouse warehouse)
            {
                if (!levels.TryGetValue((product.Id, warehouse.Id), out var level))
                {
                    level = StockLevel.Create(product, warehouse);
                    levels[(product.Id, warehouse.Id)] = level;
                }

                return level;
            }

            void OpenStock(Warehouse warehouse, (string Sku, int Quantity)[] stock)
            {
                foreach (var (sku, quantity) in stock)
                {
                    if (!products.TryGetValue(sku, out var product)) continue;

                    movements.Add(LevelFor(product, warehouse).Receive(quantity, "OPENING-001", "Opening stock"));
                }
            }

            OpenStock(main, MainOpeningStock);
            OpenStock(east, EastOpeningStock);

            // A little history after the opening stock, so the ledger shows an issue and a count too.
            if (products.TryGetValue("LAP-001", out var laptop))
            {
                movements.Add(LevelFor(laptop, main).Issue(2, "SO-DEMO-1001", "Sold to a customer"));
            }

            if (products.TryGetValue("MON-024", out var monitor))
            {
                movements.Add(LevelFor(monitor, main).Adjust(6, "Cycle count: 2 damaged units written off"));
            }

            _context.StockLevels.AddRange(levels.Values);
            _context.StockMovements.AddRange(movements);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Seeded {Levels} demo stock levels with {Movements} ledger lines.", levels.Count, movements.Count);
        }
    }
}
