using EnterpriseOMS.Application.IntegrationTests.Support;
using EnterpriseOMS.Domain.Entities.Catalog;
using EnterpriseOMS.Infrastructure.Persistence;
using EnterpriseOMS.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EnterpriseOMS.Application.IntegrationTests.Persistence;

public class ProductDemoSeederTests : IntegrationTestBase
{
    private async Task SeedAsync()
    {
        ActAnonymously();
        using var scope = App.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ProductDemoSeeder>().SeedAsync();
    }

    private async Task<List<Product>> AllProductsAsync()
    {
        ActAnonymously();
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await db.Products.AsNoTracking().OrderBy(p => p.Id).ToListAsync();
    }

    private async Task<int> DefaultOrganizationIdAsync()
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return (await db.Organizations.IgnoreQueryFilters()
            .SingleAsync(o => o.Code == IdentitySeeder.DefaultOrganizationCode)).Id;
    }

    [Fact]
    public async Task Seed_CreatesTheSampleCatalog_InTheDefaultOrganization()
    {
        await SeedAsync();

        var products = await AllProductsAsync();
        var defaultOrganizationId = await DefaultOrganizationIdAsync();

        Assert.Equal(13, products.Count);
        Assert.All(products, product => Assert.Equal(defaultOrganizationId, product.OrganizationId));
    }

    [Fact]
    public async Task Seed_IncludesExactlyOneInactiveProduct_SoTheStatusFilterHasSomethingToShow()
    {
        await SeedAsync();

        var inactive = (await AllProductsAsync()).Where(p => !p.IsActive).ToList();

        Assert.Equal("DISC-001", Assert.Single(inactive).Sku);
    }

    [Fact]
    public async Task Seed_UsesDistinctSkus()
    {
        await SeedAsync();

        var products = await AllProductsAsync();

        Assert.Equal(products.Count, products.Select(p => p.NormalizedSku).Distinct().Count());
    }

    [Fact]
    public async Task Seed_CoversSevenCategories()
    {
        await SeedAsync();

        var categories = (await AllProductsAsync())
            .Select(p => p.Category)
            .Where(category => category is not null)
            .Distinct()
            .OrderBy(category => category)
            .ToList();

        Assert.Equal(
            new[] { "Accessories", "Beverages", "Consumables", "Electronics", "Furniture", "Packaging", "Stationery" },
            categories);
    }

    [Fact]
    public async Task Seed_RunTwice_AddsNothing()
    {
        await SeedAsync();
        await SeedAsync();

        Assert.Equal(13, (await AllProductsAsync()).Count);
    }

    [Fact]
    public async Task Seed_WhenDisabledInConfiguration_AddsNothing()
    {
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var seeder = new ProductDemoSeeder(
                db,
                Options.Create(new SeedDemoDataSettings { Products = false }),
                NullLogger<ProductDemoSeeder>.Instance);

            await seeder.SeedAsync();
        }

        Assert.Empty(await AllProductsAsync());
    }

    [Fact]
    public async Task Seed_LeavesAnOrganizationThatAlreadyHasProductsAlone()
    {
        var defaultOrganizationId = await DefaultOrganizationIdAsync();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Products.Add(Product.Create(defaultOrganizationId, "MINE-1", "My own product", null, null, UnitOfMeasure.Piece, 1m, 1m, 0));
            await db.SaveChangesAsync();
        }

        await SeedAsync();

        var products = await AllProductsAsync();
        Assert.Equal("MINE-1", Assert.Single(products).Sku);
    }

    [Fact]
    public async Task Seed_DoesNotTouchOtherOrganizations()
    {
        var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
        await ActAsAsync(alice);
        await CreateProductAsync("ALICE-1");

        await SeedAsync();

        var products = await AllProductsAsync();
        var alicesOrganizationId = await OrganizationIdOfAsync(alice.UserId);

        Assert.Equal(14, products.Count);   // 13 demo products + Alice's own
        Assert.Equal(new[] { "ALICE-1" }, products.Where(p => p.OrganizationId == alicesOrganizationId).Select(p => p.Sku));
    }
}