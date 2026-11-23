using EnterpriseOMS.Application.Common.Exceptions;
using EnterpriseOMS.Application.Common.Models;
using EnterpriseOMS.Application.IntegrationTests.Support;
using EnterpriseOMS.Application.Modules.Auth.DTOs;
using EnterpriseOMS.Application.Modules.Products.Commands.SetProductActive;
using EnterpriseOMS.Application.Modules.Products.DTOs;
using EnterpriseOMS.Application.Modules.Products.Queries.GetProductById;
using EnterpriseOMS.Application.Modules.Products.Queries.GetProductCategories;
using EnterpriseOMS.Application.Modules.Products.Queries.GetProductsList;
using EnterpriseOMS.Domain.Entities.Catalog;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.IntegrationTests.Handlers
{
    public class ProductQueryHandlerTests : IntegrationTestBase
    {
        // Creation order is the order of this list. PEN-BLU is deactivated after creation.
        private static readonly (string Sku, string Name, string? Category, decimal Price)[] Catalog =
        {
        ("LAP-001", "Business Laptop", "Electronics", 899.00m),
        ("MON-024", "24-inch Monitor", "Electronics", 179.99m),
        ("KBD-010", "Wireless Keyboard", "Accessories", 39.90m),
        ("MSE-011", "Wireless Mouse", "Accessories", 24.50m),
        ("CAB-USB", "USB-C Cable", "Accessories", 9.99m),
        ("PAP-A4", "A4 Copy Paper", "Stationery", 6.75m),
        ("PEN-BLU", "Blue Pens", "Stationery", 4.20m),
        ("TON-85A", "Toner 85A", "Consumables", 64.00m),
        ("NOCAT-1", "Mystery Item", null, 1.00m),
        ("OFF-50", "50% Off Special", "Promo", 5.00m)
    };

        private readonly Dictionary<string, ProductDto> _created = new();

        // ---------- helpers ----------

        private async Task<AuthResultDto> SignInWithCatalogAsync()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            await ActAsAsync(alice);

            foreach (var (sku, name, category, price) in Catalog)
            {
                // A minute apart, so sorting by creation date has distinct values to work with.
                App.Clock.Advance(TimeSpan.FromMinutes(1));
                _created[sku] = await CreateProductAsync(sku, name, category, price);
            }

            // One inactive product, so the status filter has something to find.
            await DeactivateAsync(_created["PEN-BLU"]);

            return alice;
        }

        private async Task DeactivateAsync(ProductDto product)
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<SetProductActiveCommandHandler>();

            await handler.Handle(
                new SetProductActiveCommand { Id = product.Id, Version = product.Version, IsActive = false },
                CancellationToken.None);
        }

        private async Task<PaginatedList<ProductListItemDto>> ListAsync(Action<GetProductsListQuery>? configure = null)
        {
            var query = new GetProductsListQuery();
            configure?.Invoke(query);

            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<GetProductsListQueryHandler>();

            return await handler.Handle(query, CancellationToken.None);
        }

        private async Task<ProductDto> GetByIdAsync(int id)
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<GetProductByIdQueryHandler>();

            return await handler.Handle(new GetProductByIdQuery { Id = id }, CancellationToken.None);
        }

        private async Task<List<string>> CategoriesAsync()
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<GetProductCategoriesQueryHandler>();

            return await handler.Handle(new GetProductCategoriesQuery(), CancellationToken.None);
        }

        private static List<string> NamesOf(PaginatedList<ProductListItemDto> page) =>
            page.Items.Select(item => item.Name).ToList();

        private static List<string> SkusOf(PaginatedList<ProductListItemDto> page) =>
            page.Items.Select(item => item.Sku).ToList();

        // ---------- get by id ----------

        [Fact]
        public async Task GetById_ReturnsTheFullDetail()
        {
            await SignInWithCatalogAsync();
            var created = _created["LAP-001"];

            var product = await GetByIdAsync(created.Id);

            Assert.Equal("LAP-001", product.Sku);
            Assert.Equal("Business Laptop", product.Name);
            Assert.Equal("Electronics", product.Category);
            Assert.Equal(899.00m, product.UnitPrice);
            Assert.Equal(created.Version, product.Version);
            Assert.True(product.IsActive);
        }

        [Fact]
        public async Task GetById_UnknownId_ThrowsNotFound()
        {
            await SignInWithCatalogAsync();

            await Assert.ThrowsAsync<NotFoundException>(() => GetByIdAsync(9999));
        }

        [Fact]
        public async Task GetById_AProductOfAnotherOrganization_ThrowsNotFound()
        {
            await SignInWithCatalogAsync();
            var alicesProduct = _created["LAP-001"];

            var bob = await RegisterAsync("bob@globex.test", "Globex");
            await ActAsAsync(bob);

            await Assert.ThrowsAsync<NotFoundException>(() => GetByIdAsync(alicesProduct.Id));
        }

        // ---------- list: defaults and paging ----------

        [Fact]
        public async Task List_Default_ReturnsEveryProduct_SortedByName()
        {
            await SignInWithCatalogAsync();

            var page = await ListAsync();

            Assert.Equal(
                new[]
                {
                "24-inch Monitor", "50% Off Special", "A4 Copy Paper", "Blue Pens", "Business Laptop",
                "Mystery Item", "Toner 85A", "USB-C Cable", "Wireless Keyboard", "Wireless Mouse"
                },
                NamesOf(page));
            Assert.Equal(10, page.TotalCount);
            Assert.Equal(1, page.TotalPages);
            Assert.False(page.HasPreviousPage);
            Assert.False(page.HasNextPage);
        }

        [Fact]
        public async Task List_Paging_SplitsTheResultIntoPages()
        {
            await SignInWithCatalogAsync();

            var first = await ListAsync(q => { q.PageSize = 4; q.PageNumber = 1; });
            var second = await ListAsync(q => { q.PageSize = 4; q.PageNumber = 2; });
            var third = await ListAsync(q => { q.PageSize = 4; q.PageNumber = 3; });

            Assert.Equal(new[] { "24-inch Monitor", "50% Off Special", "A4 Copy Paper", "Blue Pens" }, NamesOf(first));
            Assert.Equal(new[] { "Business Laptop", "Mystery Item", "Toner 85A", "USB-C Cable" }, NamesOf(second));
            Assert.Equal(new[] { "Wireless Keyboard", "Wireless Mouse" }, NamesOf(third));

            Assert.Equal(10, first.TotalCount);
            Assert.Equal(3, first.TotalPages);
            Assert.False(first.HasPreviousPage);
            Assert.True(first.HasNextPage);
            Assert.True(third.HasPreviousPage);
            Assert.False(third.HasNextPage);
        }

        [Fact]
        public async Task List_APageBeyondTheEnd_HasNoItems_ButTheRealTotals()
        {
            await SignInWithCatalogAsync();

            var page = await ListAsync(q => { q.PageSize = 4; q.PageNumber = 10; });

            Assert.Empty(page.Items);
            Assert.Equal(10, page.TotalCount);
            Assert.Equal(3, page.TotalPages);
        }

        // ---------- list: search ----------

        [Theory]
        [InlineData("wireless")]
        [InlineData("WIRELESS")]
        public async Task List_Search_MatchesTheName_IgnoringCase(string search)
        {
            await SignInWithCatalogAsync();

            var page = await ListAsync(q => q.Search = search);

            Assert.Equal(new[] { "Wireless Keyboard", "Wireless Mouse" }, NamesOf(page));
            Assert.Equal(2, page.TotalCount);
        }

        [Fact]
        public async Task List_Search_MatchesPartOfTheSku()
        {
            await SignInWithCatalogAsync();

            var page = await ListAsync(q => q.Search = "usb");

            Assert.Equal(new[] { "CAB-USB" }, SkusOf(page));
        }

        [Fact]
        public async Task List_Search_TreatsAPercentSignAsALiteralCharacter()
        {
            await SignInWithCatalogAsync();

            // If "%" acted as a wildcard, this would match every product. It must match only "50% Off Special".
            var page = await ListAsync(q => q.Search = "%");

            Assert.Equal(new[] { "50% Off Special" }, NamesOf(page));
        }

        [Fact]
        public async Task List_Search_TreatsAnUnderscoreAsALiteralCharacter()
        {
            await SignInWithCatalogAsync();

            // No product contains an underscore. As a wildcard, "_" would match everything.
            var page = await ListAsync(q => q.Search = "_");

            Assert.Empty(page.Items);
        }

        [Theory]
        [InlineData("[a]")]
        [InlineData("\\")]
        [InlineData("'; DROP TABLE Products; --")]
        public async Task List_Search_SpecialCharacters_DoNotBreakTheQuery(string search)
        {
            await SignInWithCatalogAsync();

            var page = await ListAsync(q => q.Search = search);

            Assert.Empty(page.Items);
            Assert.Equal(0, page.TotalCount);

            // ...and the table is still there.
            Assert.Equal(10, (await ListAsync()).TotalCount);
        }

        // ---------- list: filters ----------

        [Fact]
        public async Task List_CategoryFilter_ReturnsOnlyThatCategory()
        {
            await SignInWithCatalogAsync();

            var page = await ListAsync(q => q.Category = "Accessories");

            Assert.Equal(new[] { "USB-C Cable", "Wireless Keyboard", "Wireless Mouse" }, NamesOf(page));
        }

        [Theory]
        [InlineData(true, 9)]
        [InlineData(false, 1)]
        [InlineData(null, 10)]
        public async Task List_StatusFilter_SeparatesActiveFromInactive(bool? isActive, int expectedCount)
        {
            await SignInWithCatalogAsync();

            var page = await ListAsync(q => q.IsActive = isActive);

            Assert.Equal(expectedCount, page.TotalCount);
        }

        [Fact]
        public async Task List_CategoryAndStatus_AreCombined()
        {
            await SignInWithCatalogAsync();

            var active = await ListAsync(q => { q.Category = "Stationery"; q.IsActive = true; });
            var inactive = await ListAsync(q => { q.Category = "Stationery"; q.IsActive = false; });

            Assert.Equal(new[] { "A4 Copy Paper" }, NamesOf(active));
            Assert.Equal(new[] { "Blue Pens" }, NamesOf(inactive));
        }

        // ---------- list: sorting ----------

        [Theory]
        [InlineData("sku", false, "CAB-USB", "TON-85A")]
        [InlineData("sku", true, "TON-85A", "CAB-USB")]
        [InlineData("price", false, "NOCAT-1", "LAP-001")]
        [InlineData("price", true, "LAP-001", "NOCAT-1")]
        [InlineData("created", false, "LAP-001", "OFF-50")]
        [InlineData("created", true, "OFF-50", "LAP-001")]
        [InlineData("name", false, "MON-024", "MSE-011")]
        [InlineData("name", true, "MSE-011", "MON-024")]
        public async Task List_Sorting_OrdersByTheChosenField(string sortBy, bool descending, string firstSku, string lastSku)
        {
            await SignInWithCatalogAsync();

            var skus = SkusOf(await ListAsync(q => { q.SortBy = sortBy; q.Descending = descending; }));

            Assert.Equal(10, skus.Count);
            Assert.Equal(firstSku, skus[0]);
            Assert.Equal(lastSku, skus[^1]);
        }

        [Fact]
        public async Task List_SortedByPrice_IsInNumericOrder_NotTextOrder()
        {
            await SignInWithCatalogAsync();

            var skus = SkusOf(await ListAsync(q => q.SortBy = "price"));

            // 1.00, 4.20, 5.00, 6.75, 9.99, 24.50, 39.90, 64.00, 179.99, 899.00
            // (sorted as text, "179.99" would wrongly come before "24.50")
            Assert.Equal(
                new[] { "NOCAT-1", "PEN-BLU", "OFF-50", "PAP-A4", "CAB-USB", "MSE-011", "KBD-010", "TON-85A", "MON-024", "LAP-001" },
                skus);
        }

        [Fact]
        public async Task List_EqualSortValues_KeepAStableOrder()
        {
            var bob = await RegisterAsync("bob@globex.test", "Globex");
            await ActAsAsync(bob);
            await CreateProductAsync("T-2", name: "Same Name");
            await CreateProductAsync("T-1", name: "Same Name");

            var ascending = SkusOf(await ListAsync(q => q.SortBy = "name"));
            var descending = SkusOf(await ListAsync(q => { q.SortBy = "name"; q.Descending = true; }));

            // Ties are broken by creation order (Id), so paging never repeats or skips a row.
            Assert.Equal(new[] { "T-2", "T-1" }, ascending);
            Assert.Equal(new[] { "T-2", "T-1" }, descending);
        }

        // ---------- list: combined, isolation, shape ----------

        [Fact]
        public async Task List_FilterSortAndPaging_WorkTogether()
        {
            await SignInWithCatalogAsync();

            var first = await ListAsync(q =>
            {
                q.Category = "Accessories";
                q.SortBy = "price";
                q.Descending = true;
                q.PageSize = 2;
                q.PageNumber = 1;
            });
            var second = await ListAsync(q =>
            {
                q.Category = "Accessories";
                q.SortBy = "price";
                q.Descending = true;
                q.PageSize = 2;
                q.PageNumber = 2;
            });

            Assert.Equal(new[] { "KBD-010", "MSE-011" }, SkusOf(first));   // 39.90, 24.50
            Assert.Equal(new[] { "CAB-USB" }, SkusOf(second));             // 9.99
            Assert.Equal(3, first.TotalCount);
            Assert.Equal(2, first.TotalPages);
        }

        [Fact]
        public async Task List_OnlyShowsTheCallersOrganization()
        {
            var alice = await SignInWithCatalogAsync();

            var bob = await RegisterAsync("bob@globex.test", "Globex");
            await ActAsAsync(bob);
            await CreateProductAsync("BOB-1", name: "Bob's Item");

            var asBob = await ListAsync();
            Assert.Equal(new[] { "BOB-1" }, SkusOf(asBob));

            await ActAsAsync(alice);
            var asAlice = await ListAsync();
            Assert.Equal(10, asAlice.TotalCount);
            Assert.DoesNotContain("BOB-1", string.Join(",", SkusOf(asAlice)));
        }

        [Fact]
        public async Task List_ItemsCarryTheFieldsATableRowNeeds()
        {
            await SignInWithCatalogAsync();
            var created = _created["LAP-001"];

            var item = (await ListAsync()).Items.Single(i => i.Sku == "LAP-001");

            Assert.Equal(created.Id, item.Id);
            Assert.Equal("Business Laptop", item.Name);
            Assert.Equal("Electronics", item.Category);
            Assert.Equal(UnitOfMeasure.Piece, item.UnitOfMeasure);
            Assert.Equal(899.00m, item.UnitPrice);
            Assert.Equal(5, item.ReorderLevel);
            Assert.True(item.IsActive);
            Assert.Equal(created.Version, item.Version);
        }

        // ---------- categories ----------

        [Fact]
        public async Task Categories_AreDistinct_Sorted_AndSkipUncategorizedProducts()
        {
            await SignInWithCatalogAsync();

            var categories = await CategoriesAsync();

            Assert.Equal(new[] { "Accessories", "Consumables", "Electronics", "Promo", "Stationery" }, categories);
        }

        [Fact]
        public async Task Categories_OnlyCoverTheCallersOrganization()
        {
            await SignInWithCatalogAsync();

            var bob = await RegisterAsync("bob@globex.test", "Globex");
            await ActAsAsync(bob);
            await CreateProductAsync("BOB-1", category: "Zeta");

            Assert.Equal(new[] { "Zeta" }, await CategoriesAsync());
        }

        [Fact]
        public async Task Categories_WhenThereAreNoProducts_IsAnEmptyList()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            await ActAsAsync(alice);

            Assert.Empty(await CategoriesAsync());
        }
    }
}
