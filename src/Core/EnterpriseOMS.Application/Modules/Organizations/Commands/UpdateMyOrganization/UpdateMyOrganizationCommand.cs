using EnterpriseOMS.Application.Modules.Organizations.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Organizations.Commands.UpdateMyOrganization
{
    public class UpdateMyOrganizationCommand : IRequest<OrganizationDto>
    {
        public string Name { get; set; } = default!;
    }
}
