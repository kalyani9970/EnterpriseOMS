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

namespace EnterpriseOMS.Application.Modules.Warehouses.Commands.UpdateStorageLocation
{
    public class UpdateStorageLocationCommandHandler : IRequestHandler<UpdateStorageLocationCommand, StorageLocationDto>
    {
        private readonly IApplicationDbContext _context;

        public UpdateStorageLocationCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<StorageLocationDto> Handle(UpdateStorageLocationCommand request, CancellationToken cancellationToken)
        {
            // The warehouse id in the URL must match the location's warehouse, or it is "not found":
            // a location can never be reached through a warehouse it does not belong to.
            var location = await _context.StorageLocations
                .FirstOrDefaultAsync(l => l.Id == request.Id && l.WarehouseId == request.WarehouseId, cancellationToken)
                ?? throw new NotFoundException(nameof(StorageLocation), request.Id);

            if (location.Version != request.Version)
            {
                throw new ConcurrencyException(nameof(StorageLocation), request.Id);
            }

            location.Rename(request.Name);

            await _context.SaveChangesAsync(cancellationToken);

            return StorageLocationDto.From(location);
        }
    }
}
