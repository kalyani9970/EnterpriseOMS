using EnterpriseOMS.Domain.Entities.Tenancy;
using EnterpriseOMS.Domain.Entities.Warehousing;
using EnterpriseOMS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Infrastructure.Persistence.Configurations
{
    public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
    {
        public void Configure(EntityTypeBuilder<Warehouse> builder)
        {
            builder.ToTable("Warehouses");

            builder.HasKey(w => w.Id);

            builder.Property(w => w.Code).IsRequired().HasMaxLength(Warehouse.CodeMaxLength);
            builder.Property(w => w.NormalizedCode).IsRequired().HasMaxLength(Warehouse.CodeMaxLength);
            builder.Property(w => w.Name).IsRequired().HasMaxLength(Warehouse.NameMaxLength);
            builder.Property(w => w.CreatedBy).HasMaxLength(64);
            builder.Property(w => w.LastModifiedBy).HasMaxLength(64);

            // Optimistic concurrency (see ProductConfiguration).
            builder.Property(w => w.Version).IsConcurrencyToken();

            // The address is a VALUE OBJECT stored in the warehouse's own row: no table, no join, and it
            // is always loaded and saved together with the warehouse. HasColumnName gives readable names.
            builder.OwnsOne(w => w.Address, address =>
            {
                address.Property(a => a.Line1).HasColumnName("AddressLine1").IsRequired().HasMaxLength(Address.LineMaxLength);
                address.Property(a => a.Line2).HasColumnName("AddressLine2").HasMaxLength(Address.LineMaxLength);
                address.Property(a => a.City).HasColumnName("City").IsRequired().HasMaxLength(Address.CityMaxLength);
                address.Property(a => a.Region).HasColumnName("Region").HasMaxLength(Address.RegionMaxLength);
                address.Property(a => a.PostalCode).HasColumnName("PostalCode").HasMaxLength(Address.PostalCodeMaxLength);
                address.Property(a => a.CountryCode).HasColumnName("CountryCode").IsRequired().HasMaxLength(2);
            });
            builder.Navigation(w => w.Address).IsRequired();

            // A code is unique WITHIN an organization (two companies can both have "WH-MAIN").
            builder.HasIndex(w => new { w.OrganizationId, w.NormalizedCode }).IsUnique();
            builder.HasIndex(w => new { w.OrganizationId, w.Name });

            builder.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(w => w.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
