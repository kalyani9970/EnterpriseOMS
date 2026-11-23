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

namespace EnterpriseOMS.Application.Modules.Warehouses.Commands.CreateStorageLocation
{
    public class CreateStorageLocationCommandHandler : IRequestHandler<CreateStorageLocationCommand, StorageLocationDto>
    {
        private const string DuplicateCodeMessage = "A location with this code already exists in this warehouse.";
        private const string ParentNotFoundMessage = "The parent location was not found in this warehouse.";

        private readonly IApplicationDbContext _context;

        public CreateStorageLocationCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<StorageLocationDto> Handle(CreateStorageLocationCommand request, CancellationToken cancellationToken)
        {
            // Both lookups go through the tenant filter, so another organization's warehouse or location
            // is simply "not found". The organization of the new location comes from the warehouse itself.
            var warehouse = await _context.Warehouses
                .FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken)
                ?? throw new NotFoundException(nameof(Warehouse), request.WarehouseId);

            StorageLocation? parent = null;

            if (request.ParentId is not null)
            {
                var parentId = request.ParentId.Value;

                parent = await _context.StorageLocations
                    .FirstOrDefaultAsync(l => l.Id == parentId && l.WarehouseId == request.WarehouseId, cancellationToken);

                if (parent is null)
                {
                    throw new ValidationException(
                        new[] { new FluentValidation.Results.ValidationFailure("ParentId", ParentNotFoundMessage) });
                }
            }

            var normalizedCode = StorageLocation.NormalizeCode(request.Code);

            if (await CodeExistsAsync(request.WarehouseId, normalizedCode, cancellationToken))
            {
                throw DuplicateCode();
            }

            // The domain checks the tree rules: same warehouse, active parent, a level below the parent,
            // an active warehouse.
            var location = StorageLocation.Create(warehouse, parent, request.Code, request.Name, request.Type);

            _context.StorageLocations.Add(location);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                if (await CodeExistsAsync(request.WarehouseId, normalizedCode, cancellationToken))
                {
                    throw DuplicateCode();
                }

                throw;
            }

            return StorageLocationDto.From(location);
        }

        private Task<bool> CodeExistsAsync(int warehouseId, string normalizedCode, CancellationToken cancellationToken) =>
            _context.StorageLocations.AnyAsync(
                l => l.WarehouseId == warehouseId && l.NormalizedCode == normalizedCode,
                cancellationToken);

        private static ValidationException DuplicateCode() =>
            new(new[] { new FluentValidation.Results.ValidationFailure("Code", DuplicateCodeMessage) });
    }
}
