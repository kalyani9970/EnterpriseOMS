using EnterpriseOMS.Application.IntegrationTests.Support;
using EnterpriseOMS.Application.Modules.Warehouses.Commands.SetWarehouseActive;
using EnterpriseOMS.Application.Modules.Warehouses.DTOs;
using EnterpriseOMS.Domain.Common.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.IntegrationTests.Handlers
{
    /// <summary>
    /// "A warehouse that still holds stock cannot be deactivated", through the real handlers and database:
    /// the warehouse handler asks the stock table how many products still have units on hand.
    /// </summary>
    public class WarehouseStockRuleTests : IntegrationTestBase
    {
        private async Task<WarehouseDto> SetActiveAsync(WarehouseDto warehouse, bool isActive)
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<SetWarehouseActiveCommandHandler>();

            return await handler.Handle(
                new SetWarehouseActiveCommand { Id = warehouse.Id, Version = warehouse.Version, IsActive = isActive },
                CancellationToken.None);
        }

        private async Task SignInAsync()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            await ActAsAsync(alice);
        }

        [Fact]
        public async Task Deactivate_WhileTheWarehouseHoldsStock_IsRefused_AndItStaysActive()
        {
            await SignInAsync();
            var warehouse = await CreateWarehouseAsync("WH-MAIN");
            var product = await CreateProductAsync("LAP-001");
            await ReceiveStockAsync(product.Id, warehouse.Id, 5);

            var exception = await Assert.ThrowsAsync<DomainException>(() => SetActiveAsync(warehouse, isActive: false));

            Assert.Equal("This warehouse still holds stock of 1 product(s). Issue or move it out first.", exception.Message);
            Assert.True((await SetActiveAsync(warehouse, isActive: true)).IsActive);   // untouched: reactivating is a harmless no-op
        }

        [Fact]
        public async Task Deactivate_CountsHowManyProductsAreStillStocked()
        {
            await SignInAsync();
            var warehouse = await CreateWarehouseAsync("WH-MAIN");
            var laptop = await CreateProductAsync("LAP-001");
            var mouse = await CreateProductAsync("MSE-011");
            var cable = await CreateProductAsync("CAB-001");
            await ReceiveStockAsync(laptop.Id, warehouse.Id, 5);
            await ReceiveStockAsync(mouse.Id, warehouse.Id, 1);
            await ReceiveStockAsync(cable.Id, warehouse.Id, 9);
            await IssueStockAsync(cable.Id, warehouse.Id, 9);   // sold out: no longer counts

            var exception = await Assert.ThrowsAsync<DomainException>(() => SetActiveAsync(warehouse, isActive: false));

            Assert.Contains("stock of 2 product(s)", exception.Message);
        }

        [Fact]
        public async Task Deactivate_AfterAllTheStockWasIssued_Succeeds()
        {
            await SignInAsync();
            var warehouse = await CreateWarehouseAsync("WH-MAIN");
            var product = await CreateProductAsync("LAP-001");
            await ReceiveStockAsync(product.Id, warehouse.Id, 5);
            await IssueStockAsync(product.Id, warehouse.Id, 5);

            var deactivated = await SetActiveAsync(warehouse, isActive: false);

            Assert.False(deactivated.IsActive);
        }

        [Fact]
        public async Task Deactivate_AfterACountOfZero_Succeeds()
        {
            await SignInAsync();
            var warehouse = await CreateWarehouseAsync("WH-MAIN");
            var product = await CreateProductAsync("LAP-001");
            await ReceiveStockAsync(product.Id, warehouse.Id, 5);
            await AdjustStockAsync(product.Id, warehouse.Id, 0, "Warehouse emptied");

            var deactivated = await SetActiveAsync(warehouse, isActive: false);

            Assert.False(deactivated.IsActive);
        }

        [Fact]
        public async Task Deactivate_IsNotBlockedByStockHeldInAnotherWarehouse()
        {
            await SignInAsync();
            var main = await CreateWarehouseAsync("WH-MAIN");
            var east = await CreateWarehouseAsync("WH-EAST");
            var product = await CreateProductAsync("LAP-001");
            await ReceiveStockAsync(product.Id, main.Id, 5);

            var deactivated = await SetActiveAsync(east, isActive: false);

            Assert.False(deactivated.IsActive);
        }

        [Fact]
        public async Task Deactivate_AWarehouseWithNoStockAtAll_Succeeds()
        {
            await SignInAsync();
            var warehouse = await CreateWarehouseAsync("WH-MAIN");

            var deactivated = await SetActiveAsync(warehouse, isActive: false);

            Assert.False(deactivated.IsActive);
        }

        [Fact]
        public async Task AfterTheStockIsCleared_TheWarehouseCanBeDeactivated_AndThenNoLongerReceives()
        {
            await SignInAsync();
            var warehouse = await CreateWarehouseAsync("WH-MAIN");
            var product = await CreateProductAsync("LAP-001");
            await ReceiveStockAsync(product.Id, warehouse.Id, 5);
            await IssueStockAsync(product.Id, warehouse.Id, 5);

            await SetActiveAsync(warehouse, isActive: false);

            var exception = await Assert.ThrowsAsync<DomainException>(
                () => ReceiveStockAsync(product.Id, warehouse.Id, 1));

            Assert.Equal("Stock cannot be received into an inactive warehouse.", exception.Message);
        }

        [Fact]
        public async Task Reactivating_AWarehouse_NeverNeedsAStockCheck()
        {
            await SignInAsync();
            var warehouse = await CreateWarehouseAsync("WH-MAIN");
            var deactivated = await SetActiveAsync(warehouse, isActive: false);

            var reactivated = await SetActiveAsync(deactivated, isActive: true);

            Assert.True(reactivated.IsActive);
        }
    }
}
