using EnterpriseOMS.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Products.Queries.GetProductCategories
{
    public class GetProductCategoriesQueryHandler : IRequestHandler<GetProductCategoriesQuery, List<string>>
    {
        private readonly IApplicationDbContext _context;

        public GetProductCategoriesQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<string>> Handle(GetProductCategoriesQuery request, CancellationToken cancellationToken)
        {
            return await _context.Products
                .AsNoTracking()
                .Where(p => p.Category != null)
                .Select(p => p.Category!)
                .Distinct()
                .OrderBy(category => category)
                .ToListAsync(cancellationToken);
        }
    }
}
