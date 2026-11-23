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

namespace EnterpriseOMS.Application.Modules.Organizations.Commands.UpdateMyOrganization
{
    public class UpdateMyOrganizationCommandHandler : IRequestHandler<UpdateMyOrganizationCommand, OrganizationDto>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;

        public UpdateMyOrganizationCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        public async Task<OrganizationDto> Handle(UpdateMyOrganizationCommand request, CancellationToken cancellationToken)
        {
            var organizationId = _currentUser.OrganizationId ?? throw new ForbiddenAccessException();

            var organization = await _context.Organizations
                .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken)
                ?? throw new NotFoundException(nameof(Organization), organizationId);

            // Business rule lives in the domain entity. The Code is intentionally NOT editable:
            // it is a stable identifier that other systems and URLs may rely on.
            organization.Rename(request.Name);

            await _context.SaveChangesAsync(cancellationToken);

            var userCount = await _context.Users
                .CountAsync(u => u.OrganizationId == organizationId, cancellationToken);

            return OrganizationDto.From(organization, userCount);
        }
    }
}
