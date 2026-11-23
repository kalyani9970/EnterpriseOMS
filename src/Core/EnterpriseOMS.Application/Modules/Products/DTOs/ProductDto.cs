using EnterpriseOMS.Domain.Entities.Catalog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Products.DTOs
{
    /// <summary>Full product detail. EF entities are never returned from the API: this is the public shape.</summary>
    public class ProductDto
    {
        public int Id { get; set; }
        public string Sku { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public string? Category { get; set; }
        public UnitOfMeasure UnitOfMeasure { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal CostPrice { get; set; }
        public int ReorderLevel { get; set; }
        public bool IsActive { get; set; }

        /// <summary>Send this back when updating. If it no longer matches, someone else changed the product first.</summary>
        public Guid Version { get; set; }

        public DateTime Created { get; set; }
        public DateTime? LastModified { get; set; }

        public static ProductDto From(Product product) => new()
        {
            Id = product.Id,
            Sku = product.Sku,
            Name = product.Name,
            Description = product.Description,
            Category = product.Category,
            UnitOfMeasure = product.UnitOfMeasure,
            UnitPrice = product.UnitPrice,
            CostPrice = product.CostPrice,
            ReorderLevel = product.ReorderLevel,
            IsActive = product.IsActive,
            Version = product.Version,
            Created = product.Created,
            LastModified = product.LastModified
        };
    }
}
