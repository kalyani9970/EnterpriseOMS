using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Application.Common.Models;
using EnterpriseOMS.Application.Common.Querying;
using EnterpriseOMS.Application.Modules.Warehouses.DTOs;
using EnterpriseOMS.Domain.Entities.Warehousing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Warehouses.Queries.GetWarehousesList
{
    public class GetWarehousesListQueryHandler : IRequestHandler<GetWarehousesListQuery, PaginatedList<WarehouseListItemDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetWarehousesListQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PaginatedList<WarehouseListItemDto>> Handle(
            GetWarehousesListQuery request,
            CancellationToken cancellationToken)
        {
            IQueryable<Warehouse> query = _context.Warehouses.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var pattern = LikeSearch.ContainsPattern(request.Search);
                query = query.Where(w =>
                    EF.Functions.Like(w.Code, pattern, LikeSearch.EscapeCharacter) ||
                    EF.Functions.Like(w.Name, pattern, LikeSearch.EscapeCharacter) ||
                    EF.Functions.Like(w.Address.City, pattern, LikeSearch.EscapeCharacter));
            }

            if (request.IsActive.HasValue)
            {
                var isActive = request.IsActive.Value;
                query = query.Where(w => w.IsActive == isActive);
            }

            query = ApplySort(query, request.SortBy, request.Descending);

            // The location count is a correlated sub-query: one SQL statement, no extra round trip per row.
            var items = query.Select(w => new WarehouseListItemDto
            {
                Id = w.Id,
                Code = w.Code,
                Name = w.Name,
                City = w.Address.City,
                CountryCode = w.Address.CountryCode,
                IsActive = w.IsActive,
                Version = w.Version,
                LocationCount = _context.StorageLocations.Count(l => l.WarehouseId == w.Id)
            });

            return await PaginatedList<WarehouseListItemDto>.CreateAsync(items, request.PageNumber, request.PageSize);
        }

        /// <summary>Whitelist-based sorting; every ordering ends with Id so equal values page in a stable order.</summary>
        private static IQueryable<Warehouse> ApplySort(IQueryable<Warehouse> query, string sortBy, bool descending)
        {
            return (sortBy.ToLowerInvariant(), descending) switch
            {
                ("code", false) => query.OrderBy(w => w.NormalizedCode).ThenBy(w => w.Id),
                ("code", true) => query.OrderByDescending(w => w.NormalizedCode).ThenBy(w => w.Id),

                ("city", false) => query.OrderBy(w => w.Address.City).ThenBy(w => w.Id),
                ("city", true) => query.OrderByDescending(w => w.Address.City).ThenBy(w => w.Id),

                ("created", false) => query.OrderBy(w => w.Created).ThenBy(w => w.Id),
                ("created", true) => query.OrderByDescending(w => w.Created).ThenBy(w => w.Id),

                (_, false) => query.OrderBy(w => w.Name).ThenBy(w => w.Id),
                (_, true) => query.OrderByDescending(w => w.Name).ThenBy(w => w.Id)
            };
        }
    }
}
