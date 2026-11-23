using EnterpriseOMS.Domain.Common.Exceptions;
using EnterpriseOMS.Domain.Entities.Catalog;
using EnterpriseOMS.Domain.Entities.Warehousing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.Entities.Inventory
{
    /// <summary>
    /// Rules that involve a product AND a warehouse, so they belong to neither entity alone. They are checked
    /// by the stock operations before anything changes.
    /// </summary>
    public static class StockRules
    {
        /// <summary>Stock only moves between things of the same organization. The tenant filter already guarantees it; this is the explicit, tested rule behind it.</summary>
        public static void EnsureSameOrganization(Product product, Warehouse warehouse)
        {
            if (product.OrganizationId != warehouse.OrganizationId)
                throw new DomainException("The product and the warehouse belong to different organizations.");
        }

        /// <summary>
        /// Receiving needs an active product and an active warehouse: nobody should buy a discontinued product
        /// or deliver into a closed warehouse. Issuing and counting stay possible for inactive ones, so existing
        /// stock can still be cleared out before a product or warehouse is retired.
        /// </summary>
        public static void EnsureCanReceive(Product product, Warehouse warehouse)
        {
            EnsureSameOrganization(product, warehouse);

            if (!product.IsActive)
                throw new DomainException("Stock cannot be received for an inactive product.");

            if (!warehouse.IsActive)
                throw new DomainException("Stock cannot be received into an inactive warehouse.");
        }
    }
}
