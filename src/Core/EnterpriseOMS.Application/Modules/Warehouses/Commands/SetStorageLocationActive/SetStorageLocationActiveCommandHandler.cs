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

namespace EnterpriseOMS.Application.Modules.Warehouses.Commands.SetStorageLocationActive
{
    public class SetStorageLocationActiveCommandHandler : IRequestHandler<SetStorageLocationActiveCommand, StorageLocationDto>
    {
        private readonly IApplicationDbContext _context;

        public SetStorageLocationActiveCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<StorageLocationDto> Handle(SetStorageLocationActiveCommand request, CancellationToken cancellationToken)
        {
            var location = await _context.StorageLocations
                .FirstOrDefaultAsync(l => l.Id == request.Id && l.WarehouseId == request.WarehouseId, cancellationToken)
                ?? throw new NotFoundException(nameof(StorageLocation), request.Id);

            if (location.Version != request.Version)
            {
                throw new ConcurrencyException(nameof(StorageLocation), request.Id);
            }

            if (request.IsActive)
            {
                // Coming back needs an active parent. The entity enforces it; we only fetch the parent.
                StorageLocation? parent = null;

                if (location.ParentId is not null)
                {
                    var parentId = location.ParentId.Value;
                    parent = await _context.StorageLocations.FirstOrDefaultAsync(l => l.Id == parentId, cancellationToken);
                }

                location.Activate(parent);
            }
            else
            {
                // Going away needs no active sub-locations. Again the entity enforces it; we only count them.
                var activeChildren = await _context.StorageLocations
                    .CountAsync(l => l.ParentId == location.Id && l.IsActive, cancellationToken);

                location.Deactivate(activeChildren);
            }

            await _context.SaveChangesAsync(cancellationToken);

            return StorageLocationDto.From(location);
        }
    }
}
