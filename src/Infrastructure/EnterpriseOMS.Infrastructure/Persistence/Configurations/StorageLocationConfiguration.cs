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
    public class StorageLocationConfiguration : IEntityTypeConfiguration<StorageLocation>
    {
        public void Configure(EntityTypeBuilder<StorageLocation> builder)
        {
            builder.ToTable("StorageLocations");

            builder.HasKey(l => l.Id);

            builder.Property(l => l.Code).IsRequired().HasMaxLength(StorageLocation.CodeMaxLength);
            builder.Property(l => l.NormalizedCode).IsRequired().HasMaxLength(StorageLocation.CodeMaxLength);
            builder.Property(l => l.Name).HasMaxLength(StorageLocation.NameMaxLength);
            builder.Property(l => l.CreatedBy).HasMaxLength(64);
            builder.Property(l => l.LastModifiedBy).HasMaxLength(64);

            // Stored as text ("Zone", "Bin"...) so the table is readable in SQL.
            builder.Property(l => l.Type).HasConversion<string>().HasMaxLength(20).IsRequired();

            builder.Property(l => l.Version).IsConcurrencyToken();

            // A code is unique WITHIN a warehouse.
            builder.HasIndex(l => new { l.WarehouseId, l.NormalizedCode }).IsUnique();
            builder.HasIndex(l => new { l.OrganizationId, l.WarehouseId });
            builder.HasIndex(l => l.ParentId);

            // Every relationship is Restrict: nothing is ever deleted by cascade. (It also avoids SQL Server's
            // "multiple cascade paths" error, which a self-referencing table would otherwise trigger.)
            builder.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(l => l.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<Warehouse>()
                .WithMany()
                .HasForeignKey(l => l.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            // The tree: a location points at the location it sits inside.
            builder.HasOne<StorageLocation>()
                .WithMany()
                .HasForeignKey(l => l.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
