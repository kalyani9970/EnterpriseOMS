using EnterpriseOMS.Application.Common.Models;
using EnterpriseOMS.Application.Modules.Warehouses.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Warehouses.Queries.GetWarehousesList
{
    /// <summary>One page of warehouses. Bound from the query string; every property has a safe default.</summary>
    public class GetWarehousesListQuery : IRequest<PaginatedList<WarehouseListItemDto>>
    {
        public const int MaxPageSize = 100;

        /// <summary>The fields a list can be sorted by. Anything else is rejected, never passed to the database.</summary>
        public static readonly string[] SortFields = { "code", "name", "city", "created" };

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;

        /// <summary>Matches part of the code, the name or the city, ignoring case.</summary>
        public string? Search { get; set; }

        /// <summary>null = both active and inactive warehouses.</summary>
        public bool? IsActive { get; set; }

        public string SortBy { get; set; } = "name";
        public bool Descending { get; set; }
    }
}
