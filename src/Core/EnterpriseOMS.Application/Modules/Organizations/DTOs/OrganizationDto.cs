using EnterpriseOMS.Domain.Entities.Tenancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Organizations.DTOs
{
    public class OrganizationDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public string Code { get; set; } = default!;
        public bool IsActive { get; set; }
        public DateTime Created { get; set; }
        public int UserCount { get; set; }

        public static OrganizationDto From(Organization organization, int userCount) => new()
        {
            Id = organization.Id,
            Name = organization.Name,
            Code = organization.Code,
            IsActive = organization.IsActive,
            Created = organization.Created,
            UserCount = userCount
        };
    }
}
