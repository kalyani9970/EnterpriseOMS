using EnterpriseOMS.Domain.Common;
using EnterpriseOMS.Domain.Common.Exceptions;
using EnterpriseOMS.Domain.Entities.Catalog;
using EnterpriseOMS.Domain.Entities.Inventory;
using EnterpriseOMS.Domain.Entities.Sales;
using EnterpriseOMS.Domain.Entities.Warehousing;
using EnterpriseOMS.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.UnitTests.Entities
{
    public class OrderTests
    {
        private static readonly DateTime Now = new(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc);

        // ---------- helpers ----------

        private static Customer MakeCustomer(int organizationId = 7, int id = 21, bool active = true)
        {
            var customer = Customer.Create(
                organizationId, "ACME-01", "Acme Retail", "buyer@acme.test", null,
                Address.Create("5 Market Street", null, "Rotterdam", null, null, "NL"));
            customer.Id = id;
            if (!active) customer.Deactivate();
            return customer;
        }

        private static Warehouse MakeWarehouse(int organizationId = 7, int id = 10, bool active = true)
        {
            var warehouse = Warehouse.Create(
                organizationId, "WH-MAIN", "Main", Address.Create("1 Test Street", null, "Utrecht", null, null, "NL"));
            warehouse.Id = id;
            if (!active) warehouse.Deactivate();
            return warehouse;
        }

        private static Product MakeProduct(string sku, int id, decimal price = 10m, int organizationId = 7, bool active = true)
        {
            var product = Product.Create(organizationId, sku, $"Product {sku}", null, null, UnitOfMeasure.Piece, price, 1m, 5);
            product.Id = id;
            if (!active) product.Deactivate();
            return product;
        }

        private static StockLevel MakeLevel(Product product, Warehouse warehouse, int onHand)
        {
            var level = StockLevel.Create(product, warehouse);
            if (onHand > 0) level.Receive(onHand);
            return level;
        }

        private static Dictionary<int, StockLevel> Levels(params StockLevel[] levels) => levels.ToDictionary(l => l.ProductId);

        private static Order MakeDraft(Customer customer, Warehouse warehouse, params (Product Product, int Quantity)[] lines) =>
            Order.CreateDraft(42, customer, warehouse, "eur", null, lines.Select(l => new OrderLineInput(l.Product, l.Quantity)));

        private static OrderLineInput Line(Product product, int quantity) => new(product, quantity);

        // ---------- creating a draft ----------

        [Fact]
        public void CreateDraft_StartsAsADraft_WithTheGivenParties_AndNoLines()
        {
            var order = MakeDraft(MakeCustomer(), MakeWarehouse());

            Assert.Equal(OrderStatus.Draft, order.Status);
            Assert.Equal(7, order.OrganizationId);
            Assert.Equal(42, order.OrderNumber);
            Assert.Equal("SO-000042", order.OrderNumberText);
            Assert.Equal(21, order.CustomerId);
            Assert.Equal(10, order.WarehouseId);
            Assert.Equal("EUR", order.CurrencyCode);
            Assert.Null(order.Notes);
            Assert.Empty(order.Lines);
            Assert.Equal(0m, order.TotalAmount);
            Assert.Equal(0, order.TotalQuantity);
            Assert.Null(order.ConfirmedAt);
            Assert.Null(order.FulfilledAt);
            Assert.Null(order.CancelledAt);
            Assert.NotEqual(Guid.Empty, order.Version);
        }

        [Fact]
        public void FormatNumber_PadsToSixDigits()
        {
            Assert.Equal("SO-000001", Order.FormatNumber(1));
            Assert.Equal("SO-123456", Order.FormatNumber(123456));
        }

        [Fact]
        public void CreateDraft_Lines_CopyTheSkuNameAndPriceFromTheProduct_AndAddUpTheTotal()
        {
            var laptop = MakeProduct("LAP-001", 3, price: 899.99m);
            var mouse = MakeProduct("MSE-011", 4, price: 25.50m);

            var order = MakeDraft(MakeCustomer(), MakeWarehouse(), (laptop, 2), (mouse, 3));

            var first = order.Lines.First();
            Assert.Equal(3, first.ProductId);
            Assert.Equal("LAP-001", first.ProductSku);
            Assert.Equal("Product LAP-001", first.ProductName);
            Assert.Equal(2, first.Quantity);
            Assert.Equal(899.99m, first.UnitPrice);
            Assert.Equal(1799.98m, first.LineTotal);
            Assert.Equal(76.50m, order.Lines.Last().LineTotal);
            Assert.Equal(1876.48m, order.TotalAmount);
            Assert.Equal(5, order.TotalQuantity);
            Assert.All(order.Lines, line => Assert.Equal(7, line.OrganizationId));
            Assert.All(order.Lines, line => Assert.Equal(0, line.ReservedQuantity));
        }

        [Fact]
        public void CreateDraft_TheSameProductTwice_BecomesOneLine_WithTheQuantitiesAddedTogether()
        {
            var laptop = MakeProduct("LAP-001", 3);

            var order = Order.CreateDraft(
                1, MakeCustomer(), MakeWarehouse(), "EUR", null, new[] { Line(laptop, 2), Line(laptop, 3) });

            var line = Assert.Single(order.Lines);
            Assert.Equal(5, line.Quantity);
        }

        [Fact]
        public void CreateDraft_TrimsTheNotes_AndTurnsBlankIntoNothing()
        {
            var withNotes = Order.CreateDraft(1, MakeCustomer(), MakeWarehouse(), "EUR", "  Deliver before Friday  ", Array.Empty<OrderLineInput>());
            var blank = Order.CreateDraft(1, MakeCustomer(), MakeWarehouse(), "EUR", "   ", Array.Empty<OrderLineInput>());

            Assert.Equal("Deliver before Friday", withNotes.Notes);
            Assert.Null(blank.Notes);
        }

        [Fact]
        public void CreateDraft_NotesOfExactly500Characters_AreAccepted_And501AreNot()
        {
            var ok = Order.CreateDraft(1, MakeCustomer(), MakeWarehouse(), "EUR", new string('n', 500), Array.Empty<OrderLineInput>());
            Assert.Equal(500, ok.Notes!.Length);

            Assert.Throws<DomainException>(
                () => Order.CreateDraft(1, MakeCustomer(), MakeWarehouse(), "EUR", new string('n', 501), Array.Empty<OrderLineInput>()));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void CreateDraft_WithoutAPositiveOrderNumber_Throws(int number)
        {
            var exception = Assert.Throws<DomainException>(
                () => Order.CreateDraft(number, MakeCustomer(), MakeWarehouse(), "EUR", null, Array.Empty<OrderLineInput>()));

            Assert.Equal("An order needs a positive order number.", exception.Message);
        }

        [Theory]
        [InlineData("")]
        [InlineData("EU")]
        [InlineData("EURO")]
        [InlineData("E1R")]
        [InlineData("€€€")]
        public void CreateDraft_WithAnInvalidCurrency_Throws(string currency)
        {
            var exception = Assert.Throws<DomainException>(
                () => Order.CreateDraft(1, MakeCustomer(), MakeWarehouse(), currency, null, Array.Empty<OrderLineInput>()));

            Assert.Equal("Currency must be a 3-letter code such as EUR or INR.", exception.Message);
        }

        [Fact]
        public void CreateDraft_UpperCasesAndTrimsTheCurrency()
        {
            var order = Order.CreateDraft(1, MakeCustomer(), MakeWarehouse(), "  inr ", null, Array.Empty<OrderLineInput>());

            Assert.Equal("INR", order.CurrencyCode);
        }

        [Fact]
        public void CreateDraft_ACustomerAndWarehouseOfDifferentOrganizations_Throws()
        {
            var exception = Assert.Throws<DomainException>(() => MakeDraft(MakeCustomer(organizationId: 7), MakeWarehouse(organizationId: 8)));

            Assert.Equal("The customer and the warehouse belong to different organizations.", exception.Message);
        }

        [Fact]
        public void CreateDraft_ForACustomerThatIsNotSavedYet_Throws()
        {
            var exception = Assert.Throws<DomainException>(() => MakeDraft(MakeCustomer(id: 0), MakeWarehouse()));

            Assert.Contains("must be saved", exception.Message);
        }

        [Fact]
        public void CreateDraft_ForAWarehouseThatIsNotSavedYet_Throws()
        {
            var exception = Assert.Throws<DomainException>(() => MakeDraft(MakeCustomer(), MakeWarehouse(id: 0)));

            Assert.Contains("must be saved", exception.Message);
        }

        [Fact]
        public void CreateDraft_ForAnInactiveCustomer_Throws()
        {
            var exception = Assert.Throws<DomainException>(() => MakeDraft(MakeCustomer(active: false), MakeWarehouse()));

            Assert.Equal("An order needs an active customer.", exception.Message);
        }

        [Fact]
        public void CreateDraft_ForAnInactiveWarehouse_Throws()
        {
            var exception = Assert.Throws<DomainException>(() => MakeDraft(MakeCustomer(), MakeWarehouse(active: false)));

            Assert.Equal("An order needs an active warehouse.", exception.Message);
        }

        [Fact]
        public void CreateDraft_WithAnInactiveProduct_Throws_NamingIt()
        {
            var discontinued = MakeProduct("OLD-001", 9, active: false);

            var exception = Assert.Throws<DomainException>(() => MakeDraft(MakeCustomer(), MakeWarehouse(), (discontinued, 1)));

            Assert.Equal("Product OLD-001 is inactive and cannot be ordered.", exception.Message);
        }

        [Fact]
        public void CreateDraft_WithAProductOfAnotherOrganization_Throws()
        {
            var foreign = MakeProduct("FOR-001", 9, organizationId: 8);

            var exception = Assert.Throws<DomainException>(() => MakeDraft(MakeCustomer(), MakeWarehouse(), (foreign, 1)));

            Assert.Equal("Product FOR-001 belongs to a different organization.", exception.Message);
        }

        [Fact]
        public void CreateDraft_WithAProductThatIsNotSavedYet_Throws()
        {
            var unsaved = MakeProduct("NEW-001", 0);

            var exception = Assert.Throws<DomainException>(() => MakeDraft(MakeCustomer(), MakeWarehouse(), (unsaved, 1)));

            Assert.Contains("must be saved", exception.Message);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-4)]
        public void CreateDraft_ANonPositiveQuantity_Throws(int quantity)
        {
            var exception = Assert.Throws<DomainException>(
                () => MakeDraft(MakeCustomer(), MakeWarehouse(), (MakeProduct("LAP-001", 3), quantity)));

            Assert.Equal("Quantity must be greater than zero.", exception.Message);
        }

        [Fact]
        public void CreateDraft_ALineQuantityUpToAMillion_IsAccepted_ButNotOneMore()
        {
            var laptop = MakeProduct("LAP-001", 3);

            Assert.Equal(1_000_000, MakeDraft(MakeCustomer(), MakeWarehouse(), (laptop, 1_000_000)).Lines.Single().Quantity);

            var exception = Assert.Throws<DomainException>(() => MakeDraft(MakeCustomer(), MakeWarehouse(), (laptop, 1_000_001)));
            Assert.Contains("cannot exceed 1,000,000 units", exception.Message);
        }

        [Fact]
        public void CreateDraft_RepeatsThatAddUpPastAMillion_Throw()
        {
            var laptop = MakeProduct("LAP-001", 3);

            Assert.Throws<DomainException>(() => MakeDraft(MakeCustomer(), MakeWarehouse(), (laptop, 600_000), (laptop, 600_000)));
        }

        [Fact]
        public void CreateDraft_Allows100Lines_ButNotMore()
        {
            var hundred = Enumerable.Range(1, 100).Select(i => MakeProduct($"P-{i}", i)).ToArray();
            var order = Order.CreateDraft(1, MakeCustomer(), MakeWarehouse(), "EUR", null, hundred.Select(p => Line(p, 1)));
            Assert.Equal(100, order.Lines.Count);

            var tooMany = hundred.Append(MakeProduct("P-101", 101)).ToArray();
            var exception = Assert.Throws<DomainException>(
                () => Order.CreateDraft(1, MakeCustomer(), MakeWarehouse(), "EUR", null, tooMany.Select(p => Line(p, 1))));
            Assert.Equal("An order can have at most 100 lines.", exception.Message);
        }

        [Fact]
        public void Order_AndItsLines_AreTenantEntities()
        {
            var order = MakeDraft(MakeCustomer(), MakeWarehouse(), (MakeProduct("LAP-001", 3), 1));

            Assert.IsAssignableFrom<ITenantEntity>(order);
            Assert.IsAssignableFrom<ITenantEntity>(order.Lines.Single());
        }

        // ---------- editing a draft ----------

        [Fact]
        public void UpdateDraft_ReplacesTheCustomer_Warehouse_Notes_AndEveryLine_AndKeepsTheCurrency()
        {
            var customer = MakeCustomer();
            var order = MakeDraft(customer, MakeWarehouse(), (MakeProduct("LAP-001", 3), 2));
            var version = order.Version;

            var otherCustomer = MakeCustomer(id: 22);
            var eastWarehouse = MakeWarehouse(id: 11);
            order.UpdateDraft(otherCustomer, eastWarehouse, "  Rush  ", new[] { Line(MakeProduct("MSE-011", 4, 25.5m), 4) });

            Assert.Equal(22, order.CustomerId);
            Assert.Equal(11, order.WarehouseId);
            Assert.Equal("Rush", order.Notes);
            Assert.Equal("EUR", order.CurrencyCode);
            var line = Assert.Single(order.Lines);
            Assert.Equal("MSE-011", line.ProductSku);
            Assert.Equal(102.0m, order.TotalAmount);
            Assert.NotEqual(version, order.Version);
        }

        [Fact]
        public void UpdateDraft_CanEmptyTheOrder()
        {
            var order = MakeDraft(MakeCustomer(), MakeWarehouse(), (MakeProduct("LAP-001", 3), 2));

            order.UpdateDraft(MakeCustomer(), MakeWarehouse(), null, Array.Empty<OrderLineInput>());

            Assert.Empty(order.Lines);
            Assert.Equal(0m, order.TotalAmount);
        }

        [Fact]
        public void UpdateDraft_OnAnOrderThatIsNotADraft_Throws()
        {
            var laptop = MakeProduct("LAP-001", 3);
            var warehouse = MakeWarehouse();
            var customer = MakeCustomer();
            var order = MakeDraft(customer, warehouse, (laptop, 2));
            order.Confirm(customer, warehouse, Levels(MakeLevel(laptop, warehouse, 10)), Now);

            var exception = Assert.Throws<DomainException>(
                () => order.UpdateDraft(customer, warehouse, null, new[] { Line(laptop, 1) }));

            Assert.Equal("Only a draft order can be changed.", exception.Message);
        }

        [Fact]
        public void UpdateDraft_WithAnInvalidLine_Throws_AndLeavesTheOrderExactlyAsItWas()
        {
            var order = Order.CreateDraft(1, MakeCustomer(), MakeWarehouse(), "EUR", "Original", new[] { Line(MakeProduct("LAP-001", 3), 2) });
            var version = order.Version;
            var discontinued = MakeProduct("OLD-001", 9, active: false);

            Assert.Throws<DomainException>(
                () => order.UpdateDraft(MakeCustomer(id: 22), MakeWarehouse(id: 11), "Changed", new[] { Line(discontinued, 1) }));

            Assert.Equal("Original", order.Notes);
            Assert.Equal(21, order.CustomerId);
            Assert.Equal(10, order.WarehouseId);
            Assert.Equal("LAP-001", order.Lines.Single().ProductSku);
            Assert.Equal(version, order.Version);
        }

        [Fact]
        public void UpdateDraft_WithACustomerOfAnotherOrganization_Throws()
        {
            var order = MakeDraft(MakeCustomer(), MakeWarehouse());

            var exception = Assert.Throws<DomainException>(
                () => order.UpdateDraft(MakeCustomer(organizationId: 8), MakeWarehouse(organizationId: 8), null, Array.Empty<OrderLineInput>()));

            Assert.Contains("different organization than the order", exception.Message);
            Assert.Equal(21, order.CustomerId);
        }

        // ---------- confirming: the stock is reserved ----------

        private sealed record Scene(Order Order, Customer Customer, Warehouse Warehouse, Product Laptop, Product Mouse, StockLevel LaptopLevel, StockLevel MouseLevel);

        /// <summary>A draft for 2 laptops and 3 mice, in a warehouse that holds 10 laptops and 8 mice.</summary>
        private static Scene MakeScene(int laptopsOnHand = 10, int miceOnHand = 8)
        {
            var customer = MakeCustomer();
            var warehouse = MakeWarehouse();
            var laptop = MakeProduct("LAP-001", 3, 899.99m);
            var mouse = MakeProduct("MSE-011", 4, 25.50m);

            return new Scene(
                MakeDraft(customer, warehouse, (laptop, 2), (mouse, 3)),
                customer, warehouse, laptop, mouse,
                MakeLevel(laptop, warehouse, laptopsOnHand),
                MakeLevel(mouse, warehouse, miceOnHand));
        }

        [Fact]
        public void Confirm_ReservesEveryLine_AndMarksTheOrderConfirmed()
        {
            var scene = MakeScene();
            var version = scene.Order.Version;

            scene.Order.Confirm(scene.Customer, scene.Warehouse, Levels(scene.LaptopLevel, scene.MouseLevel), Now);

            Assert.Equal(OrderStatus.Confirmed, scene.Order.Status);
            Assert.Equal(Now, scene.Order.ConfirmedAt);
            Assert.NotEqual(version, scene.Order.Version);
            Assert.Equal(2, scene.LaptopLevel.QuantityReserved);
            Assert.Equal(3, scene.MouseLevel.QuantityReserved);
            Assert.Equal(8, scene.LaptopLevel.QuantityAvailable);
            Assert.Equal(5, scene.MouseLevel.QuantityAvailable);
            Assert.Equal(10, scene.LaptopLevel.QuantityOnHand);   // nothing has physically moved
            Assert.Equal(new[] { 2, 3 }, scene.Order.Lines.Select(l => l.ReservedQuantity));
        }

        [Fact]
        public void Confirm_AnOrderThatIsNotADraft_Throws()
        {
            var scene = MakeScene();
            var levels = Levels(scene.LaptopLevel, scene.MouseLevel);
            scene.Order.Confirm(scene.Customer, scene.Warehouse, levels, Now);

            var exception = Assert.Throws<DomainException>(() => scene.Order.Confirm(scene.Customer, scene.Warehouse, levels, Now));

            Assert.Equal("Only a draft order can be confirmed.", exception.Message);
            Assert.Equal(2, scene.LaptopLevel.QuantityReserved);   // not reserved twice
        }

        [Fact]
        public void Confirm_AnOrderWithNoLines_Throws()
        {
            var customer = MakeCustomer();
            var warehouse = MakeWarehouse();
            var order = MakeDraft(customer, warehouse);

            var exception = Assert.Throws<DomainException>(() => order.Confirm(customer, warehouse, Levels(), Now));

            Assert.Equal("An order needs at least one line before it can be confirmed.", exception.Message);
        }

        [Fact]
        public void Confirm_ForACustomerWhoWasDeactivatedMeanwhile_Throws_AndReservesNothing()
        {
            var scene = MakeScene();
            scene.Customer.Deactivate();

            var exception = Assert.Throws<DomainException>(
                () => scene.Order.Confirm(scene.Customer, scene.Warehouse, Levels(scene.LaptopLevel, scene.MouseLevel), Now));

            Assert.Equal("The customer is inactive, so this order cannot be confirmed.", exception.Message);
            Assert.Equal(0, scene.LaptopLevel.QuantityReserved);
            Assert.Equal(OrderStatus.Draft, scene.Order.Status);
        }

        [Fact]
        public void Confirm_ForAWarehouseThatWasDeactivatedMeanwhile_Throws_AndReservesNothing()
        {
            var scene = MakeScene();
            // (a warehouse holding stock cannot be deactivated through the handler; this simulates one that was emptied and closed)
            scene.Warehouse.Deactivate();

            var exception = Assert.Throws<DomainException>(
                () => scene.Order.Confirm(scene.Customer, scene.Warehouse, Levels(scene.LaptopLevel, scene.MouseLevel), Now));

            Assert.Equal("The warehouse is inactive, so this order cannot be confirmed.", exception.Message);
            Assert.Equal(0, scene.LaptopLevel.QuantityReserved);
        }

        [Fact]
        public void Confirm_WithADifferentCustomerOrWarehouseThanTheOrdersOwn_Throws()
        {
            var scene = MakeScene();

            var exception = Assert.Throws<DomainException>(
                () => scene.Order.Confirm(MakeCustomer(id: 99), scene.Warehouse, Levels(scene.LaptopLevel, scene.MouseLevel), Now));

            Assert.Equal("The customer and the warehouse must be the ones on the order.", exception.Message);
        }

        [Fact]
        public void Confirm_WhenALineIsShort_Throws_ReservesNothing_AndNamesTheShortage()
        {
            var scene = MakeScene(laptopsOnHand: 1);   // the order needs 2 laptops
            var version = scene.Order.Version;

            var exception = Assert.Throws<DomainException>(
                () => scene.Order.Confirm(scene.Customer, scene.Warehouse, Levels(scene.LaptopLevel, scene.MouseLevel), Now));

            Assert.Equal("Not enough available stock to confirm this order: LAP-001 (1 available, 2 needed).", exception.Message);
            Assert.Equal(0, scene.LaptopLevel.QuantityReserved);
            Assert.Equal(0, scene.MouseLevel.QuantityReserved);   // the mouse line was fine, but nothing is reserved unless ALL can be
            Assert.Equal(OrderStatus.Draft, scene.Order.Status);
            Assert.Null(scene.Order.ConfirmedAt);
            Assert.Equal(version, scene.Order.Version);
        }

        [Fact]
        public void Confirm_ListsEveryShortageAtOnce_AndTreatsAMissingStockRecordAsZero()
        {
            var scene = MakeScene(laptopsOnHand: 1);

            // The warehouse has never held mice at all: there is no stock record for them.
            var exception = Assert.Throws<DomainException>(
                () => scene.Order.Confirm(scene.Customer, scene.Warehouse, Levels(scene.LaptopLevel), Now));

            Assert.Equal(
                "Not enough available stock to confirm this order: LAP-001 (1 available, 2 needed); MSE-011 (0 available, 3 needed).",
                exception.Message);
        }

        [Fact]
        public void Confirm_NeedingExactlyWhatIsAvailable_Succeeds()
        {
            var scene = MakeScene(laptopsOnHand: 2, miceOnHand: 3);

            scene.Order.Confirm(scene.Customer, scene.Warehouse, Levels(scene.LaptopLevel, scene.MouseLevel), Now);

            Assert.Equal(0, scene.LaptopLevel.QuantityAvailable);
            Assert.Equal(0, scene.MouseLevel.QuantityAvailable);
        }

        [Fact]
        public void Confirm_OnlyCountsAvailableStock_NotWhatIsAlreadyPromisedToAnotherOrder()
        {
            var scene = MakeScene(laptopsOnHand: 10);
            scene.LaptopLevel.Reserve(9);   // another order holds 9 of the 10 laptops

            var exception = Assert.Throws<DomainException>(
                () => scene.Order.Confirm(scene.Customer, scene.Warehouse, Levels(scene.LaptopLevel, scene.MouseLevel), Now));

            Assert.Contains("LAP-001 (1 available, 2 needed)", exception.Message);
        }

        [Fact]
        public void Confirm_TwoOrdersForTheSameLastUnits_OnlyTheFirstSucceeds()
        {
            var customer = MakeCustomer();
            var warehouse = MakeWarehouse();
            var laptop = MakeProduct("LAP-001", 3);
            var level = MakeLevel(laptop, warehouse, 3);
            var first = MakeDraft(customer, warehouse, (laptop, 2));
            var second = MakeDraft(customer, warehouse, (laptop, 2));

            first.Confirm(customer, warehouse, Levels(level), Now);
            var exception = Assert.Throws<DomainException>(() => second.Confirm(customer, warehouse, Levels(level), Now));

            Assert.Contains("LAP-001 (1 available, 2 needed)", exception.Message);
            Assert.Equal(2, level.QuantityReserved);
        }

        [Fact]
        public void Confirm_WithAStockLevelFromAnotherWarehouse_Throws()
        {
            var scene = MakeScene();
            var elsewhere = MakeLevel(scene.Laptop, MakeWarehouse(id: 11), 10);

            var exception = Assert.Throws<DomainException>(
                () => scene.Order.Confirm(scene.Customer, scene.Warehouse, Levels(elsewhere, scene.MouseLevel), Now));

            Assert.Equal("A stock level from a different warehouse was supplied for this order.", exception.Message);
        }

        // ---------- cancelling ----------

        private static Scene ConfirmedScene()
        {
            var scene = MakeScene();
            scene.Order.Confirm(scene.Customer, scene.Warehouse, Levels(scene.LaptopLevel, scene.MouseLevel), Now);
            return scene;
        }

        [Fact]
        public void Cancel_ADraft_JustMarksItCancelled_AndNeedsNoStockAtAll()
        {
            var scene = MakeScene();
            var version = scene.Order.Version;

            scene.Order.Cancel(Levels(), "  Customer changed their mind  ", Now);

            Assert.Equal(OrderStatus.Cancelled, scene.Order.Status);
            Assert.Equal(Now, scene.Order.CancelledAt);
            Assert.Equal("Customer changed their mind", scene.Order.CancellationReason);
            Assert.NotEqual(version, scene.Order.Version);
            Assert.Equal(0, scene.LaptopLevel.QuantityReserved);
        }

        [Fact]
        public void Cancel_AConfirmedOrder_ReleasesEveryReservation()
        {
            var scene = ConfirmedScene();

            scene.Order.Cancel(Levels(scene.LaptopLevel, scene.MouseLevel), "Out of budget", Now);

            Assert.Equal(OrderStatus.Cancelled, scene.Order.Status);
            Assert.Equal(0, scene.LaptopLevel.QuantityReserved);
            Assert.Equal(0, scene.MouseLevel.QuantityReserved);
            Assert.Equal(10, scene.LaptopLevel.QuantityAvailable);
            Assert.Equal(8, scene.MouseLevel.QuantityAvailable);
            Assert.Equal(new[] { 0, 0 }, scene.Order.Lines.Select(l => l.ReservedQuantity));
        }

        [Fact]
        public void Cancel_AConfirmedOrder_ReleasesOnlyItsOwnUnits_NotAnotherOrdersReservation()
        {
            var scene = MakeScene();
            scene.LaptopLevel.Reserve(5);   // another order already holds 5 laptops
            scene.Order.Confirm(scene.Customer, scene.Warehouse, Levels(scene.LaptopLevel, scene.MouseLevel), Now);
            Assert.Equal(7, scene.LaptopLevel.QuantityReserved);

            scene.Order.Cancel(Levels(scene.LaptopLevel, scene.MouseLevel), null, Now);

            Assert.Equal(5, scene.LaptopLevel.QuantityReserved);
        }

        [Fact]
        public void Cancel_AnOrderThatIsAlreadyCancelled_Throws()
        {
            var scene = MakeScene();
            scene.Order.Cancel(Levels(), null, Now);

            var exception = Assert.Throws<DomainException>(() => scene.Order.Cancel(Levels(), null, Now));

            Assert.Equal("This order is already cancelled.", exception.Message);
        }

        [Fact]
        public void Cancel_AFulfilledOrder_Throws()
        {
            var scene = ConfirmedScene();
            var levels = Levels(scene.LaptopLevel, scene.MouseLevel);
            scene.Order.Fulfil(levels, Now);

            var exception = Assert.Throws<DomainException>(() => scene.Order.Cancel(levels, null, Now));

            Assert.Equal("A fulfilled order cannot be cancelled: its stock has already left the warehouse.", exception.Message);
            Assert.Equal(OrderStatus.Fulfilled, scene.Order.Status);
        }

        [Fact]
        public void Cancel_ABlankReason_BecomesNothing()
        {
            var scene = MakeScene();

            scene.Order.Cancel(Levels(), "   ", Now);

            Assert.Null(scene.Order.CancellationReason);
        }

        [Fact]
        public void Cancel_AReasonOver500Characters_Throws_AndChangesNothing()
        {
            var scene = ConfirmedScene();
            var version = scene.Order.Version;

            Assert.Throws<DomainException>(() => scene.Order.Cancel(Levels(scene.LaptopLevel, scene.MouseLevel), new string('x', 501), Now));

            Assert.Equal(OrderStatus.Confirmed, scene.Order.Status);
            Assert.Equal(2, scene.LaptopLevel.QuantityReserved);
            Assert.Equal(version, scene.Order.Version);
        }

        [Fact]
        public void Cancel_AConfirmedOrderWhoseStockRecordIsMissing_Throws_AndReleasesNothing()
        {
            var scene = ConfirmedScene();

            var exception = Assert.Throws<DomainException>(() => scene.Order.Cancel(Levels(scene.LaptopLevel), null, Now));   // no mouse record

            Assert.Equal("There is no stock record for MSE-011 in the order's warehouse.", exception.Message);
            Assert.Equal(OrderStatus.Confirmed, scene.Order.Status);
            Assert.Equal(2, scene.LaptopLevel.QuantityReserved);   // the laptop line was fine, but NOTHING is released unless all can be
            Assert.Equal(3, scene.MouseLevel.QuantityReserved);
        }

        [Fact]
        public void Cancel_ACancelledOrder_CanNeverBeConfirmedAgain()
        {
            var scene = MakeScene();
            scene.Order.Cancel(Levels(), null, Now);

            var exception = Assert.Throws<DomainException>(
                () => scene.Order.Confirm(scene.Customer, scene.Warehouse, Levels(scene.LaptopLevel, scene.MouseLevel), Now));

            Assert.Equal("Only a draft order can be confirmed.", exception.Message);
        }

        // ---------- fulfilling ----------

        [Fact]
        public void Fulfil_SendsTheReservedUnitsOut_AndReturnsOneLedgerLinePerOrderLine()
        {
            var scene = ConfirmedScene();
            var version = scene.Order.Version;

            var movements = scene.Order.Fulfil(Levels(scene.LaptopLevel, scene.MouseLevel), Now);

            Assert.Equal(OrderStatus.Fulfilled, scene.Order.Status);
            Assert.Equal(Now, scene.Order.FulfilledAt);
            Assert.NotEqual(version, scene.Order.Version);

            Assert.Equal(8, scene.LaptopLevel.QuantityOnHand);
            Assert.Equal(5, scene.MouseLevel.QuantityOnHand);
            Assert.Equal(0, scene.LaptopLevel.QuantityReserved);
            Assert.Equal(0, scene.MouseLevel.QuantityReserved);

            Assert.Equal(2, movements.Count);
            Assert.Equal(new[] { -2, -3 }, movements.Select(m => m.QuantityChange));
            Assert.Equal(new[] { 8, 5 }, movements.Select(m => m.QuantityAfter));
            Assert.All(movements, m => Assert.Equal(StockMovementType.Issue, m.Type));
        }

        [Fact]
        public void Fulfil_TheLedgerLinesNameTheOrder()
        {
            var scene = ConfirmedScene();

            var movements = scene.Order.Fulfil(Levels(scene.LaptopLevel, scene.MouseLevel), Now);

            Assert.All(movements, m => Assert.Equal("SO-000042", m.Reference));
            Assert.All(movements, m => Assert.Equal("Fulfilment of order SO-000042", m.Notes));
        }

        [Fact]
        public void Fulfil_MovesEachLinesReservationIntoItsFulfilledQuantity()
        {
            var scene = ConfirmedScene();

            scene.Order.Fulfil(Levels(scene.LaptopLevel, scene.MouseLevel), Now);

            Assert.Equal(new[] { 2, 3 }, scene.Order.Lines.Select(l => l.FulfilledQuantity));
            Assert.Equal(new[] { 0, 0 }, scene.Order.Lines.Select(l => l.ReservedQuantity));
        }

        [Fact]
        public void Fulfil_LeavesOtherOrdersReservationsAlone()
        {
            var scene = MakeScene();
            scene.LaptopLevel.Reserve(5);   // another order holds 5 laptops
            scene.Order.Confirm(scene.Customer, scene.Warehouse, Levels(scene.LaptopLevel, scene.MouseLevel), Now);

            scene.Order.Fulfil(Levels(scene.LaptopLevel, scene.MouseLevel), Now);

            Assert.Equal(8, scene.LaptopLevel.QuantityOnHand);
            Assert.Equal(5, scene.LaptopLevel.QuantityReserved);   // still promised to the other order
            Assert.Equal(3, scene.LaptopLevel.QuantityAvailable);
        }

        [Fact]
        public void Fulfil_ADraft_Throws()
        {
            var scene = MakeScene();

            var exception = Assert.Throws<DomainException>(() => scene.Order.Fulfil(Levels(scene.LaptopLevel, scene.MouseLevel), Now));

            Assert.Equal("Only a confirmed order can be fulfilled.", exception.Message);
        }

        [Fact]
        public void Fulfil_ACancelledOrder_Throws()
        {
            var scene = ConfirmedScene();
            var levels = Levels(scene.LaptopLevel, scene.MouseLevel);
            scene.Order.Cancel(levels, null, Now);

            Assert.Throws<DomainException>(() => scene.Order.Fulfil(levels, Now));
        }

        [Fact]
        public void Fulfil_AnOrderThatIsAlreadyFulfilled_Throws_AndSendsNothingOutTwice()
        {
            var scene = ConfirmedScene();
            var levels = Levels(scene.LaptopLevel, scene.MouseLevel);
            scene.Order.Fulfil(levels, Now);

            Assert.Throws<DomainException>(() => scene.Order.Fulfil(levels, Now));

            Assert.Equal(8, scene.LaptopLevel.QuantityOnHand);
        }

        [Fact]
        public void Fulfil_WhenAStockRecordIsMissing_Throws_AndChangesNothing()
        {
            var scene = ConfirmedScene();
            var version = scene.Order.Version;

            Assert.Throws<DomainException>(() => scene.Order.Fulfil(Levels(scene.LaptopLevel), Now));

            Assert.Equal(OrderStatus.Confirmed, scene.Order.Status);
            Assert.Equal(10, scene.LaptopLevel.QuantityOnHand);   // the laptop line was fine, but NOTHING leaves unless ALL can
            Assert.Equal(version, scene.Order.Version);
        }

        [Fact]
        public void Fulfil_WhenTheStockHoldsLessReservedThanTheOrderNeeds_Throws_AndChangesNothing()
        {
            var scene = ConfirmedScene();
            scene.MouseLevel.Release(2);   // something went wrong elsewhere: the mice are no longer fully reserved

            var exception = Assert.Throws<DomainException>(() => scene.Order.Fulfil(Levels(scene.LaptopLevel, scene.MouseLevel), Now));

            Assert.Contains("MSE-011 is less than this order holds", exception.Message);
            Assert.Equal(10, scene.LaptopLevel.QuantityOnHand);
            Assert.Equal(OrderStatus.Confirmed, scene.Order.Status);
        }

        // ---------- the whole life ----------

        [Fact]
        public void ALifeFromDraftToFulfilled_LeavesTheStockExactlyAsExpected()
        {
            var scene = MakeScene(laptopsOnHand: 10, miceOnHand: 8);
            var levels = Levels(scene.LaptopLevel, scene.MouseLevel);

            scene.Order.Confirm(scene.Customer, scene.Warehouse, levels, Now);
            Assert.Equal(8, scene.LaptopLevel.QuantityAvailable);

            scene.Order.Fulfil(levels, Now);

            Assert.Equal(8, scene.LaptopLevel.QuantityOnHand);
            Assert.Equal(8, scene.LaptopLevel.QuantityAvailable);   // the promise became a real shipment: available is unchanged
            Assert.Equal(OrderStatus.Fulfilled, scene.Order.Status);
            Assert.Equal(1876.48m, scene.Order.TotalAmount);
        }

        [Fact]
        public void ALifeFromDraftToCancelled_LeavesTheStockExactlyAsItStarted()
        {
            var scene = MakeScene(laptopsOnHand: 10, miceOnHand: 8);
            var levels = Levels(scene.LaptopLevel, scene.MouseLevel);

            scene.Order.Confirm(scene.Customer, scene.Warehouse, levels, Now);
            scene.Order.Cancel(levels, "Changed mind", Now);

            Assert.Equal(10, scene.LaptopLevel.QuantityOnHand);
            Assert.Equal(10, scene.LaptopLevel.QuantityAvailable);
            Assert.Equal(8, scene.MouseLevel.QuantityAvailable);
        }
    }
}
