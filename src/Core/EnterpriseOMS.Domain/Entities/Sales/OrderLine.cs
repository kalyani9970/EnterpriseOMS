using EnterpriseOMS.Domain.Common;
using EnterpriseOMS.Domain.Common.Exceptions;
using EnterpriseOMS.Domain.Entities.Catalog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.Entities.Sales
{
    /// <summary>
    /// One product on an order. The SKU, the name and the PRICE are copied from the product when the line is
    /// made, so an order keeps saying what was actually sold even if the product is renamed or repriced later.
    /// Only the order creates and changes its lines.
    /// </summary>
    public class OrderLine : BaseEntity, ITenantEntity
    {
        // Required by EF Core. Lines are only ever created by their order.
        private OrderLine() { }

        public int OrganizationId { get; private set; }
        public int OrderId { get; private set; }
        public int ProductId { get; private set; }
        public string ProductSku { get; private set; } = default!;
        public string ProductName { get; private set; } = default!;
        public int Quantity { get; private set; }
        public decimal UnitPrice { get; private set; }

        /// <summary>Units of this line currently held back in the warehouse for it (only while the order is confirmed).</summary>
        public int ReservedQuantity { get; private set; }

        /// <summary>Units of this line that have left the warehouse (only once the order is fulfilled).</summary>
        public int FulfilledQuantity { get; private set; }

        public decimal LineTotal => Quantity * UnitPrice;

        internal static OrderLine Create(int organizationId, Product product, int quantity)
        {
            if (quantity <= 0)
                throw new DomainException("Quantity must be greater than zero.");

            return new OrderLine
            {
                OrganizationId = organizationId,
                ProductId = product.Id,
                ProductSku = product.Sku,
                ProductName = product.Name,
                Quantity = quantity,
                UnitPrice = product.UnitPrice
            };
        }

        internal void ReserveAll() => ReservedQuantity = Quantity;

        internal void ClearReservation() => ReservedQuantity = 0;

        internal void MarkFulfilled()
        {
            FulfilledQuantity = ReservedQuantity;
            ReservedQuantity = 0;
        }
    }
}
