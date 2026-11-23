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
    public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
    {
        public void Configure(EntityTypeBuilder<StockMovement> builder)
        {
            // A ledger line always changes the stock by SOMETHING, and never leaves it negative.
            builder.ToTable("StockMovements", table =>
            {
                table.HasCheckConstraint("CK_StockMovements_QuantityChange_NotZero", "[QuantityChange] <> 0");
                table.HasCheckConstraint("CK_StockMovements_QuantityAfter_NotNegative", "[QuantityAfter] >= 0");
            });

            builder.HasKey(m => m.Id);

            builder.Property(m => m.Reference).HasMaxLength(StockMovement.ReferenceMaxLength);
            builder.Property(m => m.Notes).HasMaxLength(StockMovement.NotesMaxLength);
            builder.Property(m => m.CreatedBy).HasMaxLength(64);
            builder.Property(m => m.LastModifiedBy).HasMaxLength(64);

            // Stored as text ("Receipt", "Issue", "Adjustment") so the table is readable in SQL.
            builder.Property(m => m.Type).HasConversion<string>().HasMaxLength(20).IsRequired();

            // The ledger is read newest-first per product and per warehouse.
            builder.HasIndex(m => new { m.OrganizationId, m.ProductId, m.Created });
            builder.HasIndex(m => new { m.OrganizationId, m.WarehouseId, m.Created });

            builder.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(m => m.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(m => m.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<Warehouse>()
                .WithMany()
                .HasForeignKey(m => m.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
