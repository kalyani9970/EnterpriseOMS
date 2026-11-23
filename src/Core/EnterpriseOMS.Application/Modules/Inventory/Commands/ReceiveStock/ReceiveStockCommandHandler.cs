using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Application.Modules.Inventory.DTOs;
using EnterpriseOMS.Domain.Entities.Inventory;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Inventory.Commands.ReceiveStock
{
    public class ReceiveStockCommandHandler : IRequestHandler<ReceiveStockCommand, StockMovementDto>
    {
        private readonly IApplicationDbContext _context;

        public ReceiveStockCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<StockMovementDto> Handle(ReceiveStockCommand request, CancellationToken cancellationToken)
        {
            var (product, warehouse) = await StockHandlerSupport.LoadAsync(
                _context, request.ProductId, request.WarehouseId, cancellationToken);

            // Receiving needs an active product and an active warehouse (issuing and counting do not).
            StockRules.EnsureCanReceive(product, warehouse);

            var level = await StockHandlerSupport.FindLevelAsync(_context, product.Id, warehouse.Id, cancellationToken);
            var isNewLevel = level is null;
            level ??= StockLevel.Create(product, warehouse);

            // The level changes its own quantity and hands back the ledger line that records the change.
            var movement = level.Receive(request.Quantity, request.Reference, request.Notes);

            if (isNewLevel)
            {
                _context.StockLevels.Add(level);
            }

            _context.StockMovements.Add(movement);

            await StockHandlerSupport.SaveAsync(_context, isNewLevel, product.Id, cancellationToken);

            return StockMovementDto.From(movement, product, warehouse);
        }
    }
}
