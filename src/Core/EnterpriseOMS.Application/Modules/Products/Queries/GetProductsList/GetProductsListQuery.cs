using EnterpriseOMS.Application.Common.Models;
using EnterpriseOMS.Application.Modules.Products.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Products.Queries.GetProductsList
{
    /// <summary>
    /// One page of products. Bound from the query string: GET /api/products?search=lap&amp;sortBy=price&amp;descending=true
    /// Every property has a safe default, so GET /api/products alone returns the first page by name.
    /// </summary>
    public class GetProductsListQuery : IRequest<PaginatedList<ProductListItemDto>>
    {
        public const int MaxPageSize = 100;

        /// <summary>The fields a list can be sorted by. Anything else is rejected, never passed to the database.</summary>
        public static readonly string[] SortFields = { "sku", "name", "price", "created" };

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;

        /// <summary>Matches part of the SKU or the name, ignoring case.</summary>
        public string? Search { get; set; }

        public string? Category { get; set; }

        /// <summary>null = both active and inactive products.</summary>
        public bool? IsActive { get; set; }

        public string SortBy { get; set; } = "name";
        public bool Descending { get; set; }
    }
}
