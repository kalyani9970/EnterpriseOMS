using EnterpriseOMS.Domain.Entities.Warehousing;
using EnterpriseOMS.Domain.ValueObjects;
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
    /// Gives the seeded "Default Organization" three sample warehouses (one of them closed) and a realistic
    /// location tree, so the warehouse screens have something to show. Idempotent: it does nothing if that
    /// organization already has any warehouse.
    /// </summary>
    public class WarehouseDemoSeeder
    {
        private readonly ApplicationDbContext _context;
        private readonly SeedDemoDataSettings _settings;
        private readonly ILogger<WarehouseDemoSeeder> _logger;

        public WarehouseDemoSeeder(
            ApplicationDbContext context,
            IOptions<SeedDemoDataSettings> options,
            ILogger<WarehouseDemoSeeder> logger)
        {
            _context = context;
            _settings = options.Value;
            _logger = logger;
        }

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            if (!_settings.Warehouses) return;

            var organization = await _context.Organizations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Code == IdentitySeeder.DefaultOrganizationCode, cancellationToken);

            if (organization is null) return;

            var alreadySeeded = await _context.Warehouses
                .IgnoreQueryFilters()
                .AnyAsync(w => w.OrganizationId == organization.Id, cancellationToken);

            if (alreadySeeded) return;

            var main = await AddWarehouseAsync(
                organization.Id, "WH-MAIN", "Main Distribution Center",
                Address.Create("100 Commerce Way", null, "Rotterdam", "South Holland", "3011 AA", "NL"),
                cancellationToken);

            var east = await AddWarehouseAsync(
                organization.Id, "WH-EAST", "East Fulfilment Hub",
                Address.Create("25 Harbour Street", "Dock 3", "Antwerp", "Flanders", "2000", "BE"),
                cancellationToken);

            var closed = await AddWarehouseAsync(
                organization.Id, "WH-OLD", "Closed Depot",
                Address.Create("7 Old Mill Lane", null, "Ghent", "East Flanders", "9000", "BE"),
                cancellationToken);

            closed.Deactivate();   // one inactive warehouse so the status filter has something to show
            await _context.SaveChangesAsync(cancellationToken);

            // Main: two zones. Zone A has two aisles, and the first aisle goes all the way down to bins.
            var zoneA = await AddLocationAsync(main, null, "A", "Ambient storage", StorageLocationType.Zone, cancellationToken);
            var aisleA1 = await AddLocationAsync(main, zoneA, "A-01", null, StorageLocationType.Aisle, cancellationToken);
            var rack = await AddLocationAsync(main, aisleA1, "A-01-R1", null, StorageLocationType.Rack, cancellationToken);
            var shelf = await AddLocationAsync(main, rack, "A-01-R1-S1", null, StorageLocationType.Shelf, cancellationToken);
            await AddLocationAsync(main, shelf, "A-01-R1-S1-B1", null, StorageLocationType.Bin, cancellationToken);
            await AddLocationAsync(main, shelf, "A-01-R1-S1-B2", null, StorageLocationType.Bin, cancellationToken);
            await AddLocationAsync(main, zoneA, "A-02", null, StorageLocationType.Aisle, cancellationToken);

            var zoneB = await AddLocationAsync(main, null, "B", "Cold storage", StorageLocationType.Zone, cancellationToken);
            await AddLocationAsync(main, zoneB, "B-01", null, StorageLocationType.Aisle, cancellationToken);

            // East: one zone with one aisle.
            var floor = await AddLocationAsync(east, null, "Z1", "Fulfilment floor", StorageLocationType.Zone, cancellationToken);
            await AddLocationAsync(east, floor, "Z1-01", null, StorageLocationType.Aisle, cancellationToken);

            _logger.LogInformation("Seeded 3 demo warehouses with 11 storage locations.");
        }

        private async Task<Warehouse> AddWarehouseAsync(
            int organizationId, string code, string name, Address address, CancellationToken cancellationToken)
        {
            var warehouse = Warehouse.Create(organizationId, code, name, address);

            _context.Warehouses.Add(warehouse);
            await _context.SaveChangesAsync(cancellationToken);   // saved now: locations need the warehouse's Id

            return warehouse;
        }

        private async Task<StorageLocation> AddLocationAsync(
            Warehouse warehouse,
            StorageLocation? parent,
            string code,
            string? name,
            StorageLocationType type,
            CancellationToken cancellationToken)
        {
            var location = StorageLocation.Create(warehouse, parent, code, name, type);

            _context.StorageLocations.Add(location);
            await _context.SaveChangesAsync(cancellationToken);   // saved now: its children need its Id

            return location;
        }
    }
}
