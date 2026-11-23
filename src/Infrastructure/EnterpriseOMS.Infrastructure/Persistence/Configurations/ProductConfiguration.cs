using EnterpriseOMS.Domain.Entities.Catalog;
using EnterpriseOMS.Domain.Entities.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Infrastructure.Persistence.Configurations
{
    public class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.ToTable("Products");

            builder.HasKey(p => p.Id);

            builder.Property(p => p.Sku).IsRequired().HasMaxLength(Product.SkuMaxLength);
            builder.Property(p => p.NormalizedSku).IsRequired().HasMaxLength(Product.SkuMaxLength);
            builder.Property(p => p.Name).IsRequired().HasMaxLength(Product.NameMaxLength);
            builder.Property(p => p.Description).HasMaxLength(Product.DescriptionMaxLength);
            builder.Property(p => p.Category).HasMaxLength(Product.CategoryMaxLength);
            builder.Property(p => p.CreatedBy).HasMaxLength(64);
            builder.Property(p => p.LastModifiedBy).HasMaxLength(64);

            // Stored as text ("Piece", "Box"...) so the table is readable in SQL, not a column of magic numbers.
            builder.Property(p => p.UnitOfMeasure).HasConversion<string>().HasMaxLength(20).IsRequired();

            // Money: exact decimal with 2 decimal places. Never float/double for money.
            builder.Property(p => p.UnitPrice).HasPrecision(18, 2);
            builder.Property(p => p.CostPrice).HasPrecision(18, 2);

            // Optimistic concurrency: EF adds "AND Version = <value we loaded>" to every UPDATE. If someone
            // else changed the row first, zero rows match and EF throws DbUpdateConcurrencyException.
            builder.Property(p => p.Version).IsConcurrencyToken();

            // A SKU is unique WITHIN an organization (two companies can both have "LAP-001").
            builder.HasIndex(p => new { p.OrganizationId, p.NormalizedSku }).IsUnique();

            // Support the list screen's default sort and its category filter.
            builder.HasIndex(p => new { p.OrganizationId, p.Name });
            builder.HasIndex(p => new { p.OrganizationId, p.Category });

            // No navigation property: Product points to its organization by id only, which keeps the
            // aggregate small. Restrict stops an organization with products from being deleted.
            builder.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(p => p.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
