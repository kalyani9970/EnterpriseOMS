using EnterpriseOMS.Application.Common.Exceptions;
using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Application.Modules.Warehouses.DTOs;
using EnterpriseOMS.Domain.Entities.Warehousing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseOMS.Application.Modules.Warehouses.Commands.SetWarehouseActive;

public class SetWarehouseActiveCommandHandler : IRequestHandler<SetWarehouseActiveCommand, WarehouseDto>
{
    private readonly IApplicationDbContext _context;

    public SetWarehouseActiveCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<WarehouseDto> Handle(SetWarehouseActiveCommand request, CancellationToken cancellationToken)
    {
        var warehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Warehouse), request.Id);

        if (warehouse.Version != request.Version)
        {
            throw new ConcurrencyException(nameof(Warehouse), request.Id);
        }

        if (request.IsActive)
        {
            warehouse.Activate();
        }
        else
        {
            // Stock held here would be stranded in a warehouse nobody can use. The entity states the rule;
            // this handler supplies the fact it needs: how many products still have units on hand here.
            var stockedProducts = await _context.StockLevels
                .CountAsync(l => l.WarehouseId == warehouse.Id && l.QuantityOnHand > 0, cancellationToken);

            warehouse.Deactivate(stockedProducts);
        }

        await _context.SaveChangesAsync(cancellationToken);

        var locationCount = await _context.StorageLocations
            .CountAsync(l => l.WarehouseId == warehouse.Id, cancellationToken);

        return WarehouseDto.From(warehouse, locationCount);
    }
}