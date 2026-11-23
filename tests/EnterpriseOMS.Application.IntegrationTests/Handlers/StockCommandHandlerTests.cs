using EnterpriseOMS.Application.Common.Exceptions;
using EnterpriseOMS.Application.IntegrationTests.Support;
using EnterpriseOMS.Application.Modules.Auth.DTOs;
using EnterpriseOMS.Application.Modules.Products.Commands.SetProductActive;
using EnterpriseOMS.Application.Modules.Products.DTOs;
using EnterpriseOMS.Application.Modules.Warehouses.Commands.SetWarehouseActive;
using EnterpriseOMS.Application.Modules.Warehouses.DTOs;
using EnterpriseOMS.Domain.Common.Exceptions;
using EnterpriseOMS.Domain.Entities.Inventory;
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
    public class StockCommandHandlerTests : IntegrationTestBase
    {
        // ---------- helpers ----------

        private sealed record Setup(AuthResultDto Alice, ProductDto Product, WarehouseDto Warehouse);

        /// <summary>Alice's organization with one product and one warehouse, and Alice signed in.</summary>
        private async Task<Setup> SetUpAsync()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            await ActAsAsync(alice);

            var product = await CreateProductAsync("LAP-001", "Business Laptop");
            var warehouse = await CreateWarehouseAsync("WH-MAIN");

            return new Setup(alice, product, warehouse);
        }

        private async Task DeactivateProductAsync(ProductDto product)
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<SetProductActiveCommandHandler>();

            await handler.Handle(
                new SetProductActiveCommand { Id = product.Id, Version = product.Version, IsActive = false },
                CancellationToken.None);
        }

        private async Task DeactivateWarehouseAsync(WarehouseDto warehouse)
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<SetWarehouseActiveCommandHandler>();

            await handler.Handle(
                new SetWarehouseActiveCommand { Id = warehouse.Id, Version = warehouse.Version, IsActive = false },
                CancellationToken.None);
        }

        /// <summary>Reads every stock level, ignoring tenant filters, so tests see exactly what is in the database.</summary>
        private async Task<List<StockLevel>> LevelsAsync()
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            return await db.StockLevels.AsNoTracking().IgnoreQueryFilters().OrderBy(l => l.Id).ToListAsync();
        }

        private async Task<List<StockMovement>> MovementsAsync()
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            return await db.StockMovements.AsNoTracking().IgnoreQueryFilters().OrderBy(m => m.Id).ToListAsync();
        }

        private async Task<int> QuantityOnHandAsync(int productId, int warehouseId)
        {
            var levels = await LevelsAsync();

            return levels.Single(l => l.ProductId == productId && l.WarehouseId == warehouseId).QuantityOnHand;
        }

        // ---------- receive ----------

        [Fact]
        public async Task Receive_CreatesTheStockLevel_AndWritesTheLedgerLine()
        {
            var setup = await SetUpAsync();

            await ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, 10);

            var level = Assert.Single(await LevelsAsync());
            Assert.Equal(10, level.QuantityOnHand);
            Assert.Equal(setup.Product.Id, level.ProductId);
            Assert.Equal(setup.Warehouse.Id, level.WarehouseId);

            var movement = Assert.Single(await MovementsAsync());
            Assert.Equal(StockMovementType.Receipt, movement.Type);
            Assert.Equal(10, movement.QuantityChange);
            Assert.Equal(10, movement.QuantityAfter);
        }

        [Fact]
        public async Task Receive_ReturnsTheLedgerLine_WithProductAndWarehouseSpelledOut()
        {
            var setup = await SetUpAsync();

            var movement = await ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, 10, "PO-5001", "First delivery");

            Assert.True(movement.Id > 0);
            Assert.Equal(setup.Product.Id, movement.ProductId);
            Assert.Equal("LAP-001", movement.ProductSku);
            Assert.Equal("Business Laptop", movement.ProductName);
            Assert.Equal(setup.Warehouse.Id, movement.WarehouseId);
            Assert.Equal("WH-MAIN", movement.WarehouseCode);
            Assert.Equal(StockMovementType.Receipt, movement.Type);
            Assert.Equal(10, movement.QuantityChange);
            Assert.Equal(10, movement.QuantityAfter);
            Assert.Equal("PO-5001", movement.Reference);
            Assert.Equal("First delivery", movement.Notes);
            Assert.Equal(App.Clock.UtcNow, movement.Created);
            Assert.Equal(setup.Alice.UserId.ToString(), movement.CreatedBy);
        }

        [Fact]
        public async Task Receive_AddsToAnExistingLevel_WithoutCreatingASecondOne()
        {
            var setup = await SetUpAsync();

            var first = await ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, 10);
            var second = await ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, 5);

            Assert.Equal(10, first.QuantityAfter);
            Assert.Equal(15, second.QuantityAfter);
            Assert.Equal(15, Assert.Single(await LevelsAsync()).QuantityOnHand);
            Assert.Equal(2, (await MovementsAsync()).Count);
        }

        [Fact]
        public async Task Receive_TheSameProductInTwoWarehouses_KeepsSeparateLevels()
        {
            var setup = await SetUpAsync();
            var east = await CreateWarehouseAsync("WH-EAST");

            await ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, 10);
            await ReceiveStockAsync(setup.Product.Id, east.Id, 4);

            Assert.Equal(10, await QuantityOnHandAsync(setup.Product.Id, setup.Warehouse.Id));
            Assert.Equal(4, await QuantityOnHandAsync(setup.Product.Id, east.Id));
        }

        [Fact]
        public async Task Receive_TwoProductsInOneWarehouse_KeepSeparateLevels()
        {
            var setup = await SetUpAsync();
            var mouse = await CreateProductAsync("MSE-011", "Wireless Mouse");

            await ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, 10);
            await ReceiveStockAsync(mouse.Id, setup.Warehouse.Id, 7);

            Assert.Equal(10, await QuantityOnHandAsync(setup.Product.Id, setup.Warehouse.Id));
            Assert.Equal(7, await QuantityOnHandAsync(mouse.Id, setup.Warehouse.Id));
        }

        [Fact]
        public async Task Receive_TrimsTheReferenceAndNotes_AndTurnsBlankIntoNothing()
        {
            var setup = await SetUpAsync();

            await ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, 1, "  PO-1  ", "   ");

            var movement = Assert.Single(await MovementsAsync());
            Assert.Equal("PO-1", movement.Reference);
            Assert.Null(movement.Notes);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public async Task Receive_ANonPositiveQuantity_IsRefused_AndSavesNothing(int quantity)
        {
            var setup = await SetUpAsync();

            var exception = await Assert.ThrowsAsync<DomainException>(
                () => ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, quantity));

            Assert.Equal("Quantity must be greater than zero.", exception.Message);
            Assert.Empty(await LevelsAsync());
            Assert.Empty(await MovementsAsync());
        }

        [Fact]
        public async Task Receive_PastTheCeiling_IsRefused_AndLeavesTheStockAsItWas()
        {
            var setup = await SetUpAsync();
            await ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, StockLevel.MaxQuantity);

            await Assert.ThrowsAsync<DomainException>(() => ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, 1));

            Assert.Equal(StockLevel.MaxQuantity, await QuantityOnHandAsync(setup.Product.Id, setup.Warehouse.Id));
            Assert.Single(await MovementsAsync());
        }

        [Fact]
        public async Task Receive_ForAnInactiveProduct_IsRefused_AndSavesNothing()
        {
            var setup = await SetUpAsync();
            await DeactivateProductAsync(setup.Product);

            var exception = await Assert.ThrowsAsync<DomainException>(
                () => ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, 5));

            Assert.Equal("Stock cannot be received for an inactive product.", exception.Message);
            Assert.Empty(await LevelsAsync());
            Assert.Empty(await MovementsAsync());
        }

        [Fact]
        public async Task Receive_IntoAnInactiveWarehouse_IsRefused_AndSavesNothing()
        {
            var setup = await SetUpAsync();
            await DeactivateWarehouseAsync(setup.Warehouse);

            var exception = await Assert.ThrowsAsync<DomainException>(
                () => ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, 5));

            Assert.Equal("Stock cannot be received into an inactive warehouse.", exception.Message);
            Assert.Empty(await LevelsAsync());
        }

        [Fact]
        public async Task Receive_ForAnUnknownProduct_ThrowsNotFound()
        {
            var setup = await SetUpAsync();

            await Assert.ThrowsAsync<NotFoundException>(() => ReceiveStockAsync(9999, setup.Warehouse.Id, 5));
        }

        [Fact]
        public async Task Receive_IntoAnUnknownWarehouse_ThrowsNotFound()
        {
            var setup = await SetUpAsync();

            await Assert.ThrowsAsync<NotFoundException>(() => ReceiveStockAsync(setup.Product.Id, 9999, 5));
        }

        [Fact]
        public async Task Receive_ForAProductOfAnotherOrganization_IsNotFound_AndSavesNothing()
        {
            var alices = await SetUpAsync();

            var bob = await RegisterAsync("bob@globex.test", "Globex");
            await ActAsAsync(bob);
            var bobsWarehouse = await CreateWarehouseAsync("WH-MAIN");

            await Assert.ThrowsAsync<NotFoundException>(() => ReceiveStockAsync(alices.Product.Id, bobsWarehouse.Id, 5));

            Assert.Empty(await LevelsAsync());
            Assert.Empty(await MovementsAsync());
        }

        [Fact]
        public async Task Receive_IntoAWarehouseOfAnotherOrganization_IsNotFound_AndSavesNothing()
        {
            var alices = await SetUpAsync();

            var bob = await RegisterAsync("bob@globex.test", "Globex");
            await ActAsAsync(bob);
            var bobsProduct = await CreateProductAsync("LAP-001", "Bob's Laptop");

            await Assert.ThrowsAsync<NotFoundException>(() => ReceiveStockAsync(bobsProduct.Id, alices.Warehouse.Id, 5));

            Assert.Empty(await LevelsAsync());
        }

        [Fact]
        public async Task Receive_WithNoOrganizationInTheToken_IsNotFound_BecauseNothingIsVisible()
        {
            var setup = await SetUpAsync();
            ActAs(userId: 1, organizationId: null);

            await Assert.ThrowsAsync<NotFoundException>(
                () => ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, 5));
        }

        // ---------- issue ----------

        [Fact]
        public async Task Issue_LowersTheStock_AndWritesANegativeLedgerLine()
        {
            var setup = await SetUpAsync();
            await ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, 10);

            var movement = await IssueStockAsync(setup.Product.Id, setup.Warehouse.Id, 3, "SO-1001", "To a customer");

            Assert.Equal(StockMovementType.Issue, movement.Type);
            Assert.Equal(-3, movement.QuantityChange);
            Assert.Equal(7, movement.QuantityAfter);
            Assert.Equal("SO-1001", movement.Reference);
            Assert.Equal("To a customer", movement.Notes);
            Assert.Equal(7, await QuantityOnHandAsync(setup.Product.Id, setup.Warehouse.Id));
            Assert.Equal(2, (await MovementsAsync()).Count);
        }

        [Fact]
        public async Task Issue_Everything_LeavesZeroOnHand_AndKeepsTheLevel()
        {
            var setup = await SetUpAsync();
            await ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, 5);

            var movement = await IssueStockAsync(setup.Product.Id, setup.Warehouse.Id, 5);

            Assert.Equal(0, movement.QuantityAfter);
            Assert.Equal(0, await QuantityOnHandAsync(setup.Product.Id, setup.Warehouse.Id));
        }

        [Fact]
        public async Task Issue_MoreThanIsOnHand_IsRefused_WithTheNumbers_AndLeavesTheStockAsItWas()
        {
            var setup = await SetUpAsync();
            await ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, 5);

            var exception = await Assert.ThrowsAsync<DomainException>(
                () => IssueStockAsync(setup.Product.Id, setup.Warehouse.Id, 6));

            Assert.Equal("Not enough stock: 5 on hand, 6 requested.", exception.Message);
            Assert.Equal(5, await QuantityOnHandAsync(setup.Product.Id, setup.Warehouse.Id));
            Assert.Single(await MovementsAsync());   // only the receipt: a refused issue leaves no ledger line
        }

        [Fact]
        public async Task Issue_FromAProductThatWasNeverReceivedHere_IsRefused_AndCreatesNothing()
        {
            var setup = await SetUpAsync();

            var exception = await Assert.ThrowsAsync<DomainException>(
                () => IssueStockAsync(setup.Product.Id, setup.Warehouse.Id, 1));

            Assert.Equal("Not enough stock: 0 on hand, 1 requested.", exception.Message);
            Assert.Empty(await LevelsAsync());
            Assert.Empty(await MovementsAsync());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Issue_ANonPositiveQuantity_IsRefused(int quantity)
        {
            var setup = await SetUpAsync();
            await ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, 5);

            var exception = await Assert.ThrowsAsync<DomainException>(
                () => IssueStockAsync(setup.Product.Id, setup.Warehouse.Id, quantity));

            Assert.Equal("Quantity must be greater than zero.", exception.Message);
            Assert.Equal(5, await QuantityOnHandAsync(setup.Product.Id, setup.Warehouse.Id));
        }

        [Fact]
        public async Task Issue_AReferenceThatIsTooLong_IsRefused_AndLeavesTheStockAsItWas()
        {
            var setup = await SetUpAsync();
            await ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, 5);

            await Assert.ThrowsAsync<DomainException>(
                () => IssueStockAsync(setup.Product.Id, setup.Warehouse.Id, 1, new string('x', 101)));

            Assert.Equal(5, await QuantityOnHandAsync(setup.Product.Id, setup.Warehouse.Id));
            Assert.Single(await MovementsAsync());
        }

        [Fact]
        public async Task Issue_ExistingStockOfAnInactiveProduct_IsStillAllowed_SoItCanBeClearedOut()
        {
            var setup = await SetUpAsync();
            await ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, 5);
            await DeactivateProductAsync(setup.Product);

            var movement = await IssueStockAsync(setup.Product.Id, setup.Warehouse.Id, 2);

            Assert.Equal(3, movement.QuantityAfter);
        }

        [Fact]
        public async Task Issue_FromAnInactiveWarehouse_IsStillAllowed_SoItCanBeClearedOut()
        {
            var setup = await SetUpAsync();
            await ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, 5);

            // A warehouse cannot be deactivated through the handler while it holds stock (that is tested separately),
            // so simulate older data by deactivating the entity directly.
            using (var scope = App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var warehouse = await db.Warehouses.SingleAsync(w => w.Id == setup.Warehouse.Id);
                warehouse.Deactivate();
                await db.SaveChangesAsync();
            }

            var movement = await IssueStockAsync(setup.Product.Id, setup.Warehouse.Id, 5);

            Assert.Equal(0, movement.QuantityAfter);
        }

        [Fact]
        public async Task Issue_ForAnUnknownProduct_ThrowsNotFound()
        {
            var setup = await SetUpAsync();

            await Assert.ThrowsAsync<NotFoundException>(() => IssueStockAsync(9999, setup.Warehouse.Id, 1));
        }

        [Fact]
        public async Task Issue_ForAProductOfAnotherOrganization_IsNotFound_AndLeavesTheirStockAlone()
        {
            var alices = await SetUpAsync();
            await ReceiveStockAsync(alices.Product.Id, alices.Warehouse.Id, 5);

            var bob = await RegisterAsync("bob@globex.test", "Globex");
            await ActAsAsync(bob);
            var bobsWarehouse = await CreateWarehouseAsync("WH-MAIN");

            await Assert.ThrowsAsync<NotFoundException>(() => IssueStockAsync(alices.Product.Id, bobsWarehouse.Id, 1));

            Assert.Equal(5, await QuantityOnHandAsync(alices.Product.Id, alices.Warehouse.Id));
        }

        // ---------- adjust (a stock count) ----------

        [Fact]
        public async Task Adjust_ACountAboveTheSystemsNumber_RaisesTheStock_AndLogsAPositiveChange()
        {
            var setup = await SetUpAsync();
            await ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, 10);

            var movement = await AdjustStockAsync(setup.Product.Id, setup.Warehouse.Id, 13, "Found 3 in the back room");

            Assert.Equal(StockMovementType.Adjustment, movement.Type);
            Assert.Equal(3, movement.QuantityChange);
            Assert.Equal(13, movement.QuantityAfter);
            Assert.Equal(13, await QuantityOnHandAsync(setup.Product.Id, setup.Warehouse.Id));
        }

        [Fact]
        public async Task Adjust_ACountBelowTheSystemsNumber_LowersTheStock_AndLogsANegativeChange()
        {
            var setup = await SetUpAsync();
            await ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, 10);

            var movement = await AdjustStockAsync(setup.Product.Id, setup.Warehouse.Id, 6, "Two damaged, two missing");

            Assert.Equal(-4, movement.QuantityChange);
            Assert.Equal(6, movement.QuantityAfter);
            Assert.Equal(6, await QuantityOnHandAsync(setup.Product.Id, setup.Warehouse.Id));
        }

        [Fact]
        public async Task Adjust_ToZero_IsAValidCount()
        {
            var setup = await SetUpAsync();
            await ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, 10);

            var movement = await AdjustStockAsync(setup.Product.Id, setup.Warehouse.Id, 0, "Shelf found empty");

            Assert.Equal(-10, movement.QuantityChange);
            Assert.Equal(0, await QuantityOnHandAsync(setup.Product.Id, setup.Warehouse.Id));
        }

        [Fact]
        public async Task Adjust_StoresTheReasonAsTheNotes_AndNoReference()
        {
            var setup = await SetUpAsync();
            await ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, 10);

            var movement = await AdjustStockAsync(setup.Product.Id, setup.Warehouse.Id, 12, "  Cycle count  ");

            Assert.Equal("Cycle count", movement.Notes);
            Assert.Null(movement.Reference);
        }

        [Fact]
        public async Task Adjust_AProductThatWasNeverReceivedHere_CreatesTheLevel()
        {
            var setup = await SetUpAsync();

            var movement = await AdjustStockAsync(setup.Product.Id, setup.Warehouse.Id, 7, "Found on the shelf, never booked in");

            Assert.Equal(7, movement.QuantityChange);
            Assert.Equal(7, movement.QuantityAfter);
            Assert.Equal(7, await QuantityOnHandAsync(setup.Product.Id, setup.Warehouse.Id));
        }

        [Fact]
        public async Task Adjust_ToTheSameNumberAsTheSystem_IsRefused_AndWritesNoLedgerLine()
        {
            var setup = await SetUpAsync();
            await ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, 10);

            var exception = await Assert.ThrowsAsync<DomainException>(
                () => AdjustStockAsync(setup.Product.Id, setup.Warehouse.Id, 10));

            Assert.Contains("Nothing to adjust", exception.Message);
            Assert.Single(await MovementsAsync());
        }

        [Fact]
        public async Task Adjust_ACountOfZeroForAProductNeverStocked_IsRefused_AndCreatesNoLevel()
        {
            var setup = await SetUpAsync();

            await Assert.ThrowsAsync<DomainException>(
                () => AdjustStockAsync(setup.Product.Id, setup.Warehouse.Id, 0));

            Assert.Empty(await LevelsAsync());
            Assert.Empty(await MovementsAsync());
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Adjust_WithoutAReason_IsRefused_AndSavesNothing(string reason)
        {
            var setup = await SetUpAsync();
            await ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, 10);

            var exception = await Assert.ThrowsAsync<DomainException>(
                () => AdjustStockAsync(setup.Product.Id, setup.Warehouse.Id, 12, reason));

            Assert.Equal("A reason is required for a stock adjustment.", exception.Message);
            Assert.Equal(10, await QuantityOnHandAsync(setup.Product.Id, setup.Warehouse.Id));
            Assert.Single(await MovementsAsync());
        }

        [Fact]
        public async Task Adjust_ANegativeCount_IsRefused()
        {
            var setup = await SetUpAsync();
            await ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, 10);

            await Assert.ThrowsAsync<DomainException>(
                () => AdjustStockAsync(setup.Product.Id, setup.Warehouse.Id, -1));

            Assert.Equal(10, await QuantityOnHandAsync(setup.Product.Id, setup.Warehouse.Id));
        }

        [Fact]
        public async Task Adjust_ExistingStockOfAnInactiveProduct_IsStillAllowed()
        {
            var setup = await SetUpAsync();
            await ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, 10);
            await DeactivateProductAsync(setup.Product);

            var movement = await AdjustStockAsync(setup.Product.Id, setup.Warehouse.Id, 8, "Year-end count");

            Assert.Equal(8, movement.QuantityAfter);
        }

        [Fact]
        public async Task Adjust_ForAnUnknownProduct_ThrowsNotFound()
        {
            var setup = await SetUpAsync();

            await Assert.ThrowsAsync<NotFoundException>(() => AdjustStockAsync(9999, setup.Warehouse.Id, 5));
        }

        [Fact]
        public async Task Adjust_ForAWarehouseOfAnotherOrganization_IsNotFound_AndSavesNothing()
        {
            var alices = await SetUpAsync();

            var bob = await RegisterAsync("bob@globex.test", "Globex");
            await ActAsAsync(bob);
            var bobsProduct = await CreateProductAsync("LAP-001", "Bob's Laptop");

            await Assert.ThrowsAsync<NotFoundException>(() => AdjustStockAsync(bobsProduct.Id, alices.Warehouse.Id, 5));

            Assert.Empty(await LevelsAsync());
        }

        // ---------- the ledger as a whole ----------

        [Fact]
        public async Task TheLedger_AfterManyOperations_RunsAsABalance_AndItsChangesSumToTheQuantity()
        {
            var setup = await SetUpAsync();

            await ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, 10);
            await IssueStockAsync(setup.Product.Id, setup.Warehouse.Id, 3);
            await AdjustStockAsync(setup.Product.Id, setup.Warehouse.Id, 6);
            await ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, 4);
            await IssueStockAsync(setup.Product.Id, setup.Warehouse.Id, 10);

            var lines = await MovementsAsync();

            Assert.Equal(new[] { 10, 7, 6, 10, 0 }, lines.Select(m => m.QuantityAfter));
            Assert.Equal(new[] { 10, -3, -1, 4, -10 }, lines.Select(m => m.QuantityChange));
            Assert.Equal(0, await QuantityOnHandAsync(setup.Product.Id, setup.Warehouse.Id));
            Assert.Equal(await QuantityOnHandAsync(setup.Product.Id, setup.Warehouse.Id), lines.Sum(m => m.QuantityChange));
        }

        [Fact]
        public async Task EveryLedgerLine_BelongsToTheCallersOrganization_AndNamesWhoMadeIt()
        {
            var setup = await SetUpAsync();
            var organizationId = await OrganizationIdOfAsync(setup.Alice.UserId);

            await ReceiveStockAsync(setup.Product.Id, setup.Warehouse.Id, 10);
            await IssueStockAsync(setup.Product.Id, setup.Warehouse.Id, 2);

            var lines = await MovementsAsync();

            Assert.All(lines, line => Assert.Equal(organizationId, line.OrganizationId));
            Assert.All(lines, line => Assert.Equal(setup.Alice.UserId.ToString(), line.CreatedBy));
        }
    }
}
