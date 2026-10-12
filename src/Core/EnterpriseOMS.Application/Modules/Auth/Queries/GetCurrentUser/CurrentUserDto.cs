using AutoMapper;
using EnterpriseOMS.Application.Common.Mappings;
using EnterpriseOMS.Domain.Entities.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Auth.Queries.GetCurrentUser
{
    public class CurrentUserDto : IMapFrom<User>
    {
        public int Id { get; set; }
        public string FullName { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string Role { get; set; } = default!;
        public int OrganizationId { get; set; }
        public List<string> Permissions { get; set; } = new();

        public void Mapping(Profile profile)
        {
            profile.CreateMap<User, CurrentUserDto>()
                .ForMember(d => d.Role, opt => opt.MapFrom(s => s.Role.Name))
                .ForMember(d => d.Permissions, opt => opt.MapFrom(
                    s => s.Role.RolePermissions.Select(rp => rp.Permission.Code).ToList()));
        }
    }
}
