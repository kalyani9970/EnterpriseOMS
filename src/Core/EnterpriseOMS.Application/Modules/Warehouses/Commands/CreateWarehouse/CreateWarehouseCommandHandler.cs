using EnterpriseOMS.Application.Common.Exceptions;
using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Application.Modules.Warehouses.DTOs;
using EnterpriseOMS.Domain.Entities.Warehousing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Warehouses.Commands.CreateWarehouse
{
    public class CreateWarehouseCommandHandler : IRequestHandler<CreateWarehouseCommand, WarehouseDto>
    {
        private const string DuplicateCodeMessage = "A warehouse with this code already exists.";

        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;

        public CreateWarehouseCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        public async Task<WarehouseDto> Handle(CreateWarehouseCommand request, CancellationToken cancellationToken)
        {
            // The owner is ALWAYS the caller's organization, taken from the token (never from the request).
            var organizationId = _currentUser.OrganizationId ?? throw new ForbiddenAccessException();

            var normalizedCode = Warehouse.NormalizeCode(request.Code);

            // The tenant filter makes this check per organization: two companies may use the same code.
            if (await CodeExistsAsync(normalizedCode, cancellationToken))
            {
                throw DuplicateCode();
            }

            var warehouse = Warehouse.Create(organizationId, request.Code, request.Name, request.Address.ToAddress());

            _context.Warehouses.Add(warehouse);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Two people creating the same code at the same instant can both pass the check above;
                // the unique index then rejects the loser. Turn that into the same friendly message.
                if (await CodeExistsAsync(normalizedCode, cancellationToken))
                {
                    throw DuplicateCode();
                }

                throw;
            }

            return WarehouseDto.From(warehouse, locationCount: 0);
        }

        private Task<bool> CodeExistsAsync(string normalizedCode, CancellationToken cancellationToken) =>
            _context.Warehouses.AnyAsync(w => w.NormalizedCode == normalizedCode, cancellationToken);

        private static ValidationException DuplicateCode() =>
            new(new[] { new FluentValidation.Results.ValidationFailure("Code", DuplicateCodeMessage) });
    }
}
