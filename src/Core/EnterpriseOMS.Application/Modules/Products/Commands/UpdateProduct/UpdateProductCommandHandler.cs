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

namespace EnterpriseOMS.Application.Modules.Products.Commands.UpdateProduct
{
    public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, ProductDto>
    {
        private readonly IApplicationDbContext _context;

        public UpdateProductCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ProductDto> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
        {
            // The tenant filter means a product of another organization simply is not found (404),
            // exactly as if it did not exist. We never confirm that someone else's product exists.
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
                ?? throw new NotFoundException(nameof(Product), request.Id);

            // Optimistic concurrency, layer 1: the caller edited an older copy. Stop with a clear message.
            // Layer 2 is the database: if someone saves between this line and SaveChanges, EF's concurrency
            // token on Version rejects the UPDATE (DbUpdateConcurrencyException, also reported as 409).
            if (product.Version != request.Version)
            {
                throw new ConcurrencyException(nameof(Product), request.Id);
            }

            product.Update(
                request.Name,
                request.Description,
                request.Category,
                request.UnitOfMeasure,
                request.UnitPrice,
                request.CostPrice,
                request.ReorderLevel);

            await _context.SaveChangesAsync(cancellationToken);

            return ProductDto.From(product);
        }
    }
}
