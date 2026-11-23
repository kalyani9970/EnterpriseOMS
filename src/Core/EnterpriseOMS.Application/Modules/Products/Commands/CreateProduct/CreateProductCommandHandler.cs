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

namespace EnterpriseOMS.Application.Modules.Products.Commands.CreateProduct
{
    public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, ProductDto>
    {
        private const string DuplicateSkuMessage = "A product with this SKU already exists.";

        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;

        public CreateProductCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        public async Task<ProductDto> Handle(CreateProductCommand request, CancellationToken cancellationToken)
        {
            // The owner is ALWAYS the caller's organization, taken from the token. There is no
            // OrganizationId in the request, so a caller cannot create a product for someone else.
            var organizationId = _currentUser.OrganizationId ?? throw new ForbiddenAccessException();

            var normalizedSku = Product.NormalizeSku(request.Sku);

            // The tenant filter makes this check per organization: two companies may use the same SKU.
            if (await SkuExistsAsync(normalizedSku, cancellationToken))
            {
                throw DuplicateSku();
            }

            var product = Product.Create(
                organizationId,
                request.Sku,
                request.Name,
                request.Description,
                request.Category,
                request.UnitOfMeasure,
                request.UnitPrice,
                request.CostPrice,
                request.ReorderLevel);

            _context.Products.Add(product);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Two people creating the same SKU at the same instant can both pass the check above;
                // the unique index then rejects the loser. Turn that into the same friendly message.
                if (await SkuExistsAsync(normalizedSku, cancellationToken))
                {
                    throw DuplicateSku();
                }

                throw;
            }

            return ProductDto.From(product);
        }

        private Task<bool> SkuExistsAsync(string normalizedSku, CancellationToken cancellationToken) =>
            _context.Products.AnyAsync(p => p.NormalizedSku == normalizedSku, cancellationToken);

        private static ValidationException DuplicateSku() =>
            new(new[] { new FluentValidation.Results.ValidationFailure("Sku", DuplicateSkuMessage) });
    }
}
