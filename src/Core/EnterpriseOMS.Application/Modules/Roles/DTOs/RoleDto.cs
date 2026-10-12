using AutoMapper;
using EnterpriseOMS.Application.Common.Mappings;
using EnterpriseOMS.Domain.Entities.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Roles.DTOs
{
    public class RoleDto : IMapFrom<Role>
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public bool IsSystemRole { get; set; }
        public List<string> Permissions { get; set; } = new();

        public void Mapping(Profile profile)
        {
            profile.CreateMap<Role, RoleDto>()
                .ForMember(d => d.Permissions, opt => opt.MapFrom(
                    s => s.RolePermissions.Select(rp => rp.Permission.Code)));
        }
    }
}
