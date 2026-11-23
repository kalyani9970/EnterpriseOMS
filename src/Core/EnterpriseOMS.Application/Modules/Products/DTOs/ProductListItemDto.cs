using EnterpriseOMS.Domain.Entities.Catalog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Products.DTOs
{
    /// <summary>The slim shape used in lists: only what a table row needs, so list queries stay fast.</summary>
    public class ProductListItemDto
    {
        public int Id { get; set; }
        public string Sku { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string? Category { get; set; }
        public UnitOfMeasure UnitOfMeasure { get; set; }
        public decimal UnitPrice { get; set; }
        public int ReorderLevel { get; set; }
        public bool IsActive { get; set; }
        public Guid Version { get; set; }
    }
}
