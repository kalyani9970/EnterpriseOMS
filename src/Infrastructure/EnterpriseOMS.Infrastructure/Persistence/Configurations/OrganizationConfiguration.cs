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
    public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
    {
        public void Configure(EntityTypeBuilder<Organization> builder)
        {
            builder.ToTable("Organizations");

            builder.HasKey(o => o.Id);

            builder.Property(o => o.Name).IsRequired().HasMaxLength(Organization.NameMaxLength);
            builder.Property(o => o.Code).IsRequired().HasMaxLength(Organization.CodeMaxLength);
            builder.Property(o => o.CreatedBy).HasMaxLength(64);
            builder.Property(o => o.LastModifiedBy).HasMaxLength(64);

            // The unique index is the real guarantee behind the registration handler's -2, -3 suffixing.
            builder.HasIndex(o => o.Code).IsUnique();
        }
    }
}
