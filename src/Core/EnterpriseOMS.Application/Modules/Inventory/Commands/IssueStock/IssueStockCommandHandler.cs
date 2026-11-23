using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Application.Modules.Inventory.DTOs;
using EnterpriseOMS.Domain.Common.Exceptions;
using EnterpriseOMS.Domain.Entities.Inventory;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Inventory.Commands.IssueStock
{
    public class IssueStockCommandHandler : IRequestHandler<IssueStockCommand, StockMovementDto>
    {
        private readonly IApplicationDbContext _context;

        public IssueStockCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<StockMovementDto> Handle(IssueStockCommand request, CancellationToken cancellationToken)
        {
            var (product, warehouse) = await StockHandlerSupport.LoadAsync(
                _context, request.ProductId, request.WarehouseId, cancellationToken);

            StockRules.EnsureSameOrganization(product, warehouse);

            var level = await StockHandlerSupport.FindLevelAsync(_context, product.Id, warehouse.Id, cancellationToken);

            // A product that was never received here has no level: it is simply zero on hand.
            if (level is null)
            {
                throw new DomainException($"Not enough stock: 0 on hand, {request.Quantity} requested.");
            }

            var movement = level.Issue(request.Quantity, request.Reference, request.Notes);

            _context.StockMovements.Add(movement);

            await StockHandlerSupport.SaveAsync(_context, creatingLevel: false, product.Id, cancellationToken);

            return StockMovementDto.From(movement, product, warehouse);
        }
    }
}
