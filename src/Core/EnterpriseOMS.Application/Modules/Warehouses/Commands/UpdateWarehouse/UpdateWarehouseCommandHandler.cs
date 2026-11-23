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

namespace EnterpriseOMS.Application.Modules.Warehouses.Commands.UpdateWarehouse
{
    public class UpdateWarehouseCommandHandler : IRequestHandler<UpdateWarehouseCommand, WarehouseDto>
    {
        private readonly IApplicationDbContext _context;

        public UpdateWarehouseCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<WarehouseDto> Handle(UpdateWarehouseCommand request, CancellationToken cancellationToken)
        {
            // Another organization's warehouse is simply "not found" (the tenant filter hides it).
            var warehouse = await _context.Warehouses
                .FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken)
                ?? throw new NotFoundException(nameof(Warehouse), request.Id);

            // Optimistic concurrency: the caller edited an older copy. (EF's token on Version is the second
            // line of defence against a save that races between this check and SaveChanges.)
            if (warehouse.Version != request.Version)
            {
                throw new ConcurrencyException(nameof(Warehouse), request.Id);
            }

            warehouse.Update(request.Name, request.Address.ToAddress());

            await _context.SaveChangesAsync(cancellationToken);

            var locationCount = await _context.StorageLocations
                .CountAsync(l => l.WarehouseId == warehouse.Id, cancellationToken);

            return WarehouseDto.From(warehouse, locationCount);
        }
    }
}
