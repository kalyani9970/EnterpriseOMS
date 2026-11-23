using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Products.Queries.GetProductCategories
{
    /// <summary>The distinct categories in use, for the filter drop-down. Always scoped to the caller's organization.</summary>
    public class GetProductCategoriesQuery : IRequest<List<string>>
    {
    }
}
