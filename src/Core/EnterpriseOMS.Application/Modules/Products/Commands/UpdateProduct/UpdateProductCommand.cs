using EnterpriseOMS.Application.Modules.Products.DTOs;
using EnterpriseOMS.Domain.Entities.Catalog;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Products.Commands.UpdateProduct
{
    /// <summary>There is no Sku here on purpose: a SKU never changes after the product is created.</summary>
    public class UpdateProductCommand : IRequest<ProductDto>
    {
        public int Id { get; set; }

        /// <summary>The Version the caller last saw. It proves the edit is based on the current data.</summary>
        public Guid Version { get; set; }

        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public string? Category { get; set; }
        public UnitOfMeasure UnitOfMeasure { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal CostPrice { get; set; }
        public int ReorderLevel { get; set; }
    }
}
