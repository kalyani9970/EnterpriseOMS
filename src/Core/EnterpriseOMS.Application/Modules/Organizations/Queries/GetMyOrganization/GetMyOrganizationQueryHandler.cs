using EnterpriseOMS.Application.Common.Exceptions;
using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Application.Modules.Organizations.DTOs;
using EnterpriseOMS.Domain.Entities.Tenancy;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Organizations.Queries.GetMyOrganization
{
    public class GetMyOrganizationQueryHandler : IRequestHandler<GetMyOrganizationQuery, OrganizationDto>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;

        public GetMyOrganizationQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        public async Task<OrganizationDto> Handle(GetMyOrganizationQuery request, CancellationToken cancellationToken)
        {
            // The organization comes from the token, never from the request. A caller cannot ask
            // for someone else's organization. The global query filter enforces the same rule again.
            var organizationId = _currentUser.OrganizationId ?? throw new ForbiddenAccessException();

            var organization = await _context.Organizations
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken)
                ?? throw new NotFoundException(nameof(Organization), organizationId);

            var userCount = await _context.Users
                .CountAsync(u => u.OrganizationId == organizationId, cancellationToken);

            return OrganizationDto.From(organization, userCount);
        }
    }
}
