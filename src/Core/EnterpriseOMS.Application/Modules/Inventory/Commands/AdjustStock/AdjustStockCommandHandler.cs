using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Application.Modules.Inventory.DTOs;
using EnterpriseOMS.Domain.Entities.Inventory;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Inventory.Commands.AdjustStock
{
    public class AdjustStockCommandHandler : IRequestHandler<AdjustStockCommand, StockMovementDto>
    {
        private readonly IApplicationDbContext _context;

        public AdjustStockCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<StockMovementDto> Handle(AdjustStockCommand request, CancellationToken cancellationToken)
        {
            var (product, warehouse) = await StockHandlerSupport.LoadAsync(
                _context, request.ProductId, request.WarehouseId, cancellationToken);

            StockRules.EnsureSameOrganization(product, warehouse);

            var level = await StockHandlerSupport.FindLevelAsync(_context, product.Id, warehouse.Id, cancellationToken);
            var isNewLevel = level is null;

            // Counting a product that was never received here is allowed: the system said zero, the shelf says otherwise.
            level ??= StockLevel.Create(product, warehouse);

            var movement = level.Adjust(request.CountedQuantity, request.Reason);

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
