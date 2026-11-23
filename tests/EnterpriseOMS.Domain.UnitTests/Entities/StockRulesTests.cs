using EnterpriseOMS.Domain.Common.Exceptions;
using EnterpriseOMS.Domain.Entities.Catalog;
using EnterpriseOMS.Domain.Entities.Inventory;
using EnterpriseOMS.Domain.Entities.Warehousing;
using EnterpriseOMS.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.UnitTests.Entities
{
    public class StockRulesTests
    {
        private static Warehouse MakeWarehouse(int organizationId = 7)
        {
            var warehouse = Warehouse.Create(
                organizationId, "WH", "Main", Address.Create("1 Test Street", null, "Utrecht", null, null, "NL"));
            warehouse.Id = 10;
            return warehouse;
        }

        private static Product MakeProduct(int organizationId = 7)
        {
            var product = Product.Create(organizationId, "LAP-001", "Laptop", null, null, UnitOfMeasure.Piece, 10m, 5m, 5);
            product.Id = 3;
            return product;
        }

        [Fact]
        public void EnsureSameOrganization_ForThingsOfTheSameOrganization_Passes()
        {
            StockRules.EnsureSameOrganization(MakeProduct(7), MakeWarehouse(7));
        }

        [Fact]
        public void EnsureSameOrganization_ForThingsOfDifferentOrganizations_Throws()
        {
            var exception = Assert.Throws<DomainException>(
                () => StockRules.EnsureSameOrganization(MakeProduct(7), MakeWarehouse(8)));

            Assert.Equal("The product and the warehouse belong to different organizations.", exception.Message);
        }

        [Fact]
        public void EnsureCanReceive_AnActiveProductInAnActiveWarehouse_Passes()
        {
            StockRules.EnsureCanReceive(MakeProduct(), MakeWarehouse());
        }

        [Fact]
        public void EnsureCanReceive_AnInactiveProduct_Throws()
        {
            var product = MakeProduct();
            product.Deactivate();

            var exception = Assert.Throws<DomainException>(() => StockRules.EnsureCanReceive(product, MakeWarehouse()));

            Assert.Equal("Stock cannot be received for an inactive product.", exception.Message);
        }

        [Fact]
        public void EnsureCanReceive_AnInactiveWarehouse_Throws()
        {
            var warehouse = MakeWarehouse();
            warehouse.Deactivate();

            var exception = Assert.Throws<DomainException>(() => StockRules.EnsureCanReceive(MakeProduct(), warehouse));

            Assert.Equal("Stock cannot be received into an inactive warehouse.", exception.Message);
        }

        [Fact]
        public void EnsureCanReceive_ThingsOfDifferentOrganizations_Throws()
        {
            var exception = Assert.Throws<DomainException>(
                () => StockRules.EnsureCanReceive(MakeProduct(7), MakeWarehouse(8)));

            Assert.Contains("different organizations", exception.Message);
        }
    }
}
