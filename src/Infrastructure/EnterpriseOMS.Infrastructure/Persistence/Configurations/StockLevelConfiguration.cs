using EnterpriseOMS.Domain.Entities.Catalog;
using EnterpriseOMS.Domain.Entities.Inventory;
using EnterpriseOMS.Domain.Entities.Tenancy;
using EnterpriseOMS.Domain.Entities.Warehousing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Infrastructure.Persistence.Configurations
{
    public class StockLevelConfiguration : IEntityTypeConfiguration<StockLevel>
    {
        public void Configure(EntityTypeBuilder<StockLevel> builder)
        {
            // The last line of defence for the most important number in the system: even if every layer above
            // it had a bug, the database itself refuses a negative quantity.
            builder.ToTable("StockLevels", table =>
                table.HasCheckConstraint("CK_StockLevels_QuantityOnHand_NotNegative", "[QuantityOnHand] >= 0"));

            builder.HasKey(l => l.Id);

            builder.Property(l => l.CreatedBy).HasMaxLength(64);
            builder.Property(l => l.LastModifiedBy).HasMaxLength(64);

            // Optimistic concurrency: two people changing the same stock at the same moment cannot both win.
            builder.Property(l => l.Version).IsConcurrencyToken();

            // One level per product and warehouse. (This is also what stops two simultaneous first receipts
            // from both creating a level.)
            builder.HasIndex(l => new { l.WarehouseId, l.ProductId }).IsUnique();
            builder.HasIndex(l => new { l.OrganizationId, l.ProductId });

            // Every relationship is Restrict: nothing is ever deleted by cascade.
            builder.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(l => l.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(l => l.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<Warehouse>()
                .WithMany()
                .HasForeignKey(l => l.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
