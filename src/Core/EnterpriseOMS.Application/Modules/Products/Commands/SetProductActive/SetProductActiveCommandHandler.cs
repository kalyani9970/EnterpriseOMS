using EnterpriseOMS.Application.Common.Exceptions;
using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Application.Modules.Products.DTOs;
using EnterpriseOMS.Domain.Entities.Catalog;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Products.Commands.SetProductActive
{
    public class SetProductActiveCommandHandler : IRequestHandler<SetProductActiveCommand, ProductDto>
    {
        private readonly IApplicationDbContext _context;

        public SetProductActiveCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ProductDto> Handle(SetProductActiveCommand request, CancellationToken cancellationToken)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
                ?? throw new NotFoundException(nameof(Product), request.Id);

            if (product.Version != request.Version)
            {
                throw new ConcurrencyException(nameof(Product), request.Id);
            }

            if (request.IsActive) product.Activate();
            else product.Deactivate();

            await _context.SaveChangesAsync(cancellationToken);

            return ProductDto.From(product);
        }
    }
}
