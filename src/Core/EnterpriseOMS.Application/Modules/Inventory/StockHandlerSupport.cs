using EnterpriseOMS.Application.Common.Exceptions;
using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Domain.Entities.Catalog;
using EnterpriseOMS.Domain.Entities.Inventory;
using EnterpriseOMS.Domain.Entities.Warehousing;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseOMS.Application.Modules.Inventory;

/// <summary>
/// The steps the three stock commands share, so each handler reads as just its own business logic.
/// </summary>
internal static class StockHandlerSupport
{
    /// <summary>
    /// Loads the product and the warehouse. Both lookups go through the tenant filter, so another
    /// organization's product or warehouse is simply "not found".
    /// </summary>
    public static async Task<(Product Product, Warehouse Warehouse)> LoadAsync(
        IApplicationDbContext context,
        int productId,
        int warehouseId,
        CancellationToken cancellationToken)
    {
        var product = await context.Products
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), productId);

        var warehouse = await context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == warehouseId, cancellationToken)
            ?? throw new NotFoundException(nameof(Warehouse), warehouseId);

        return (product, warehouse);
    }

    public static Task<StockLevel?> FindLevelAsync(
        IApplicationDbContext context,
        int productId,
        int warehouseId,
        CancellationToken cancellationToken) =>
        context.StockLevels.FirstOrDefaultAsync(
            l => l.ProductId == productId && l.WarehouseId == warehouseId,
            cancellationToken);

    /// <summary>
    /// Saves the level and its ledger line together (one SaveChanges is one transaction: both or neither).
    /// If somebody else changed the same stock at the same moment, the database rejects this save. That is
    /// reported as a conflict the client can answer by trying again. It is NOT retried here: a stock COUNT
    /// is an absolute number, and silently re-applying it over a concurrent change would hide the conflict.
    /// </summary>
    public static async Task SaveAsync(
        IApplicationDbContext context,
        bool creatingLevel,
        int productId,
        CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyException("Stock", productId);
        }
        catch (DbUpdateException) when (creatingLevel)
        {
            // Two first receipts of the same product into the same warehouse at the same instant: both saw
            // "no level yet", and the unique index let only one create it.
            throw new ConcurrencyException("Stock", productId);
        }
    }
}