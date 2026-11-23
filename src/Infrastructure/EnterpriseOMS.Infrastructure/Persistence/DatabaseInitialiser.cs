using EnterpriseOMS.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseOMS.Infrastructure.Persistence;

public static class DatabaseInitialiser
{
    /// <summary>
    /// Applies pending migrations, then seeds reference data and (in Development) sample data.
    /// Called once from Program.cs at startup, and only in Development. A scope is created manually
    /// because this runs outside any HTTP request, and DbContext is a Scoped service.
    /// Applying migrations at startup is convenient for development. In production you would
    /// typically run migrations as a separate deployment step instead.
    /// </summary>
    public static async Task InitialiseDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();

        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<ProductDemoSeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<WarehouseDemoSeeder>().SeedAsync();

        // Stock needs the products and the warehouses above, so it is seeded last.
        await scope.ServiceProvider.GetRequiredService<InventoryDemoSeeder>().SeedAsync();
    }
}