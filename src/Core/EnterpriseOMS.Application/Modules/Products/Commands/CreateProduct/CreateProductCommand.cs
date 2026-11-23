using EnterpriseOMS.Application.Modules.Products.DTOs;
using EnterpriseOMS.Domain.Entities.Catalog;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Products.Commands.CreateProduct
{
    public class CreateProductCommand : IRequest<ProductDto>
    {
        public string Sku { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public string? Category { get; set; }
        public UnitOfMeasure UnitOfMeasure { get; set; } = UnitOfMeasure.Piece;
        public decimal UnitPrice { get; set; }
        public decimal CostPrice { get; set; }
        public int ReorderLevel { get; set; }
    }
}
