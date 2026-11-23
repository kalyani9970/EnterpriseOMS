using EnterpriseOMS.Application.Modules.Organizations.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Organizations.Queries.GetMyOrganization
{
    /// <summary>Returns the signed-in user's own organization. There is deliberately no "by id" variant.</summary>
    public class GetMyOrganizationQuery : IRequest<OrganizationDto>
    {
    }
}
