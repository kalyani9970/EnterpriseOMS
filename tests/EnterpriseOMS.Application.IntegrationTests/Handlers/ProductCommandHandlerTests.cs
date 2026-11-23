using EnterpriseOMS.Application.Common.Exceptions;
using EnterpriseOMS.Application.IntegrationTests.Support;
using EnterpriseOMS.Application.Modules.Auth.DTOs;
using EnterpriseOMS.Application.Modules.Products.Commands.CreateProduct;
using EnterpriseOMS.Application.Modules.Products.Commands.SetProductActive;
using EnterpriseOMS.Application.Modules.Products.Commands.UpdateProduct;
using EnterpriseOMS.Application.Modules.Products.DTOs;
using EnterpriseOMS.Domain.Common.Exceptions;
using EnterpriseOMS.Domain.Entities.Catalog;
using EnterpriseOMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.IntegrationTests.Handlers
{
    public class ProductCommandHandlerTests : IntegrationTestBase
    {
        // ---------- helpers ----------

        private static CreateProductCommand NewProduct(string sku = "LAP-001") => new()
        {
            Sku = sku,
            Name = "Business Laptop",
            Description = "14 inch",
            Category = "Electronics",
            UnitOfMeasure = UnitOfMeasure.Piece,
            UnitPrice = 899.00m,
            CostPrice = 690.00m,
            ReorderLevel = 5
        };

        private async Task<AuthResultDto> SignInAsync(string email = "alice@acme.test", string organization = "Acme Traders")
        {
            var registered = await RegisterAsync(email, organization);
            await ActAsAsync(registered);
            return registered;
        }

        private async Task<ProductDto> CreateAsync(CreateProductCommand command)
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<CreateProductCommandHandler>();

            return await handler.Handle(command, CancellationToken.None);
        }

        /// <summary>Builds an update from the product as the caller last saw it, then lets the test change fields.</summary>
        private async Task<ProductDto> UpdateAsync(ProductDto current, Action<UpdateProductCommand>? change = null)
        {
            var command = new UpdateProductCommand
            {
                Id = current.Id,
                Version = current.Version,
                Name = current.Name,
                Description = current.Description,
                Category = current.Category,
                UnitOfMeasure = current.UnitOfMeasure,
                UnitPrice = current.UnitPrice,
                CostPrice = current.CostPrice,
                ReorderLevel = current.ReorderLevel
            };
            change?.Invoke(command);

            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<UpdateProductCommandHandler>();

            return await handler.Handle(command, CancellationToken.None);
        }

        private async Task<ProductDto> SetActiveAsync(int id, Guid version, bool isActive)
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<SetProductActiveCommandHandler>();

            return await handler.Handle(
                new SetProductActiveCommand { Id = id, Version = version, IsActive = isActive },
                CancellationToken.None);
        }

        /// <summary>Reads the stored row, ignoring tenant filters, so tests can check what really is in the database.</summary>
        private async Task<Product> LoadAsync(int id)
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            return await db.Products.AsNoTracking().IgnoreQueryFilters().SingleAsync(p => p.Id == id);
        }

        private async Task<int> CountProductsAsync()
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            return await db.Products.IgnoreQueryFilters().CountAsync();
        }

        // ---------- create ----------

        [Fact]
        public async Task Create_StoresTheProductInTheCallersOrganization()
        {
            var alice = await SignInAsync();
            var organizationId = await OrganizationIdOfAsync(alice.UserId);

            var created = await CreateAsync(NewProduct());

            var stored = await LoadAsync(created.Id);
            Assert.Equal(organizationId, stored.OrganizationId);
            Assert.Equal("LAP-001", stored.Sku);
            Assert.Equal("Business Laptop", stored.Name);
        }

        [Fact]
        public async Task Create_ReturnsTheFullDetails_IncludingVersionAndCreatedDate()
        {
            await SignInAsync();

            var created = await CreateAsync(NewProduct());

            Assert.True(created.Id > 0);
            Assert.Equal("LAP-001", created.Sku);
            Assert.Equal("Business Laptop", created.Name);
            Assert.Equal("14 inch", created.Description);
            Assert.Equal("Electronics", created.Category);
            Assert.Equal(UnitOfMeasure.Piece, created.UnitOfMeasure);
            Assert.Equal(899.00m, created.UnitPrice);
            Assert.Equal(690.00m, created.CostPrice);
            Assert.Equal(5, created.ReorderLevel);
            Assert.True(created.IsActive);
            Assert.NotEqual(Guid.Empty, created.Version);
            Assert.Equal(App.Clock.UtcNow, created.Created);
            Assert.Null(created.LastModified);
        }

        [Fact]
        public async Task Create_KeepsTheSkuAsTyped_ButTrimsIt()
        {
            await SignInAsync();

            var created = await CreateAsync(NewProduct("  lap-001  "));

            Assert.Equal("lap-001", created.Sku);
            Assert.Equal("LAP-001", (await LoadAsync(created.Id)).NormalizedSku);
        }

        [Theory]
        [InlineData("LAP-001")]
        [InlineData("lap-001")]
        [InlineData("  Lap-001  ")]
        public async Task Create_DuplicateSku_IsRejected_AndSavesNothingMore(string duplicateSku)
        {
            await SignInAsync();
            await CreateAsync(NewProduct("LAP-001"));

            var exception = await Assert.ThrowsAsync<ValidationException>(() => CreateAsync(NewProduct(duplicateSku)));

            Assert.Equal("A product with this SKU already exists.", Assert.Single(exception.Errors["Sku"]));
            Assert.Equal(1, await CountProductsAsync());
        }

        [Fact]
        public async Task Create_TheSameSkuInAnotherOrganization_IsAllowed()
        {
            await SignInAsync("alice@acme.test", "Acme Traders");
            await CreateAsync(NewProduct("LAP-001"));

            await SignInAsync("bob@globex.test", "Globex");
            await CreateAsync(NewProduct("LAP-001"));

            Assert.Equal(2, await CountProductsAsync());
        }

        [Fact]
        public async Task Create_WithNoOrganizationInTheToken_ThrowsForbidden()
        {
            ActAs(userId: 1, organizationId: null);

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateAsync(NewProduct()));
        }

        [Fact]
        public async Task Create_InvalidData_ThrowsADomainException_AndSavesNothing()
        {
            await SignInAsync();
            var command = NewProduct();
            command.UnitPrice = -1m;   // the validators would stop this earlier; the entity must refuse it too

            await Assert.ThrowsAsync<DomainException>(() => CreateAsync(command));

            Assert.Equal(0, await CountProductsAsync());
        }

        [Fact]
        public async Task Create_RecordsWhoCreatedIt()
        {
            var alice = await SignInAsync();

            var created = await CreateAsync(NewProduct());

            Assert.Equal(alice.UserId.ToString(), (await LoadAsync(created.Id)).CreatedBy);
        }

        // ---------- update ----------

        [Fact]
        public async Task Update_ChangesTheDetails_AndIssuesANewVersion()
        {
            await SignInAsync();
            var created = await CreateAsync(NewProduct());

            var updated = await UpdateAsync(created, c =>
            {
                c.Name = "  Laptop Pro  ";
                c.UnitPrice = 999.00m;
                c.Category = "Computers";
            });

            Assert.Equal("Laptop Pro", updated.Name);
            Assert.Equal(999.00m, updated.UnitPrice);
            Assert.Equal("Computers", updated.Category);
            Assert.NotEqual(created.Version, updated.Version);

            var stored = await LoadAsync(created.Id);
            Assert.Equal("Laptop Pro", stored.Name);
            Assert.Equal(updated.Version, stored.Version);
        }

        [Fact]
        public async Task Update_NeverChangesTheSku()
        {
            await SignInAsync();
            var created = await CreateAsync(NewProduct("lap-001"));

            await UpdateAsync(created, c => c.Name = "Renamed");

            var stored = await LoadAsync(created.Id);
            Assert.Equal("lap-001", stored.Sku);
            Assert.Equal("LAP-001", stored.NormalizedSku);
        }

        [Fact]
        public async Task Update_WithAStaleVersion_IsRejected_AndChangesNothing()
        {
            await SignInAsync();
            var created = await CreateAsync(NewProduct());

            var first = await UpdateAsync(created, c => c.Name = "First edit");

            // The second editor still holds the ORIGINAL copy ("created"), so their edit is based on old data.
            var exception = await Assert.ThrowsAsync<ConcurrencyException>(
                () => UpdateAsync(created, c => c.Name = "Second edit, based on the old copy"));

            Assert.Contains("was changed by someone else", exception.Message);

            var stored = await LoadAsync(created.Id);
            Assert.Equal("First edit", stored.Name);
            Assert.Equal(first.Version, stored.Version);
        }

        [Fact]
        public async Task Update_WithTheLatestVersion_CanBeRepeated()
        {
            await SignInAsync();
            var created = await CreateAsync(NewProduct());

            var first = await UpdateAsync(created, c => c.Name = "First");
            var second = await UpdateAsync(first, c => c.Name = "Second");

            Assert.Equal("Second", second.Name);
            Assert.NotEqual(first.Version, second.Version);
        }

        [Fact]
        public async Task Update_UnknownProduct_ThrowsNotFound()
        {
            await SignInAsync();
            var ghost = new ProductDto { Id = 9999, Version = Guid.NewGuid(), Name = "Ghost" };

            await Assert.ThrowsAsync<NotFoundException>(() => UpdateAsync(ghost));
        }

        [Fact]
        public async Task Update_AProductOfAnotherOrganization_IsNotFound_AndStaysUntouched()
        {
            await SignInAsync("alice@acme.test", "Acme Traders");
            var alicesProduct = await CreateAsync(NewProduct());

            await SignInAsync("bob@globex.test", "Globex");

            await Assert.ThrowsAsync<NotFoundException>(() => UpdateAsync(alicesProduct, c => c.Name = "Hacked"));

            var stored = await LoadAsync(alicesProduct.Id);
            Assert.Equal("Business Laptop", stored.Name);
            Assert.Equal(alicesProduct.Version, stored.Version);
        }

        [Fact]
        public async Task Update_InvalidData_ThrowsADomainException_AndKeepsTheStoredValues()
        {
            await SignInAsync();
            var created = await CreateAsync(NewProduct());

            await Assert.ThrowsAsync<DomainException>(() => UpdateAsync(created, c => c.UnitPrice = -5m));

            var stored = await LoadAsync(created.Id);
            Assert.Equal(899.00m, stored.UnitPrice);
            Assert.Equal(created.Version, stored.Version);
        }

        [Fact]
        public async Task Update_StampsLastModified_AndWhoDidIt()
        {
            var alice = await SignInAsync();
            var created = await CreateAsync(NewProduct());
            App.Clock.Advance(TimeSpan.FromMinutes(5));
            var editedAt = App.Clock.UtcNow;

            await UpdateAsync(created, c => c.Name = "Edited");

            var stored = await LoadAsync(created.Id);
            Assert.Equal(editedAt, stored.LastModified);
            Assert.Equal(alice.UserId.ToString(), stored.LastModifiedBy);
            Assert.Equal(created.Created, stored.Created);
        }

        [Fact]
        public async Task Update_ABlankCategory_ClearsIt()
        {
            await SignInAsync();
            var created = await CreateAsync(NewProduct());

            await UpdateAsync(created, c => c.Category = "   ");

            Assert.Null((await LoadAsync(created.Id)).Category);
        }

        // ---------- activate / deactivate ----------

        [Fact]
        public async Task Deactivate_FlipsTheFlag_AndIssuesANewVersion()
        {
            await SignInAsync();
            var created = await CreateAsync(NewProduct());

            var deactivated = await SetActiveAsync(created.Id, created.Version, isActive: false);

            Assert.False(deactivated.IsActive);
            Assert.NotEqual(created.Version, deactivated.Version);

            var stored = await LoadAsync(created.Id);
            Assert.False(stored.IsActive);
            Assert.Equal(deactivated.Version, stored.Version);
        }

        [Fact]
        public async Task Reactivate_BringsAnInactiveProductBack()
        {
            await SignInAsync();
            var created = await CreateAsync(NewProduct());
            var deactivated = await SetActiveAsync(created.Id, created.Version, isActive: false);

            var reactivated = await SetActiveAsync(created.Id, deactivated.Version, isActive: true);

            Assert.True(reactivated.IsActive);
            Assert.True((await LoadAsync(created.Id)).IsActive);
        }

        [Fact]
        public async Task Deactivate_WhenAlreadyInactive_KeepsTheVersion()
        {
            await SignInAsync();
            var created = await CreateAsync(NewProduct());
            var deactivated = await SetActiveAsync(created.Id, created.Version, isActive: false);

            var again = await SetActiveAsync(created.Id, deactivated.Version, isActive: false);

            Assert.False(again.IsActive);
            Assert.Equal(deactivated.Version, again.Version);
        }

        [Fact]
        public async Task Deactivate_WithAStaleVersion_IsRejected()
        {
            await SignInAsync();
            var created = await CreateAsync(NewProduct());
            await UpdateAsync(created, c => c.Name = "Someone else edited it first");

            await Assert.ThrowsAsync<ConcurrencyException>(
                () => SetActiveAsync(created.Id, created.Version, isActive: false));

            Assert.True((await LoadAsync(created.Id)).IsActive);
        }

        [Fact]
        public async Task Deactivate_UnknownProduct_ThrowsNotFound()
        {
            await SignInAsync();

            await Assert.ThrowsAsync<NotFoundException>(
                () => SetActiveAsync(9999, Guid.NewGuid(), isActive: false));
        }

        [Fact]
        public async Task Deactivate_AProductOfAnotherOrganization_IsNotFound_AndStaysActive()
        {
            await SignInAsync("alice@acme.test", "Acme Traders");
            var alicesProduct = await CreateAsync(NewProduct());

            await SignInAsync("bob@globex.test", "Globex");

            await Assert.ThrowsAsync<NotFoundException>(
                () => SetActiveAsync(alicesProduct.Id, alicesProduct.Version, isActive: false));

            Assert.True((await LoadAsync(alicesProduct.Id)).IsActive);
        }
    }
}
