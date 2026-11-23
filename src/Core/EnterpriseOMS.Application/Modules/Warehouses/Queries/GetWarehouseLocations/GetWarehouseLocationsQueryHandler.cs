using EnterpriseOMS.Application.Common.Exceptions;
using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Application.Modules.Warehouses.DTOs;
using EnterpriseOMS.Domain.Entities.Warehousing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Warehouses.Queries.GetWarehouseLocations
{
    public class GetWarehouseLocationsQueryHandler : IRequestHandler<GetWarehouseLocationsQuery, List<StorageLocationDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetWarehouseLocationsQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<StorageLocationDto>> Handle(GetWarehouseLocationsQuery request, CancellationToken cancellationToken)
        {
            var warehouseExists = await _context.Warehouses
                .AnyAsync(w => w.Id == request.WarehouseId, cancellationToken);

            if (!warehouseExists)
            {
                throw new NotFoundException(nameof(Warehouse), request.WarehouseId);
            }

            IQueryable<StorageLocation> query = _context.StorageLocations
                .AsNoTracking()
                .Where(l => l.WarehouseId == request.WarehouseId);

            // A parent can only be inactive when all its children are (the domain forbids otherwise), so
            // filtering inactive rows out never leaves an active child without its parent in the result.
            if (request.ActiveOnly)
            {
                query = query.Where(l => l.IsActive);
            }

            var locations = await query
                .OrderBy(l => l.NormalizedCode)
                .ThenBy(l => l.Id)
                .ToListAsync(cancellationToken);

            return InTreeOrder(locations);
        }

        /// <summary>
        /// Arranges a flat list into tree order: each location is followed directly by its children, which
        /// are followed by THEIR children, and so on. The client then only needs ParentId to indent the rows.
        /// A tree is at most five levels deep (one per location type), so simple recursion is safe.
        /// </summary>
        private static List<StorageLocationDto> InTreeOrder(List<StorageLocation> locations)
        {
            var childrenOf = locations.ToLookup(location => location.ParentId);
            var ordered = new List<StorageLocationDto>(locations.Count);

            void Visit(int? parentId)
            {
                foreach (var location in childrenOf[parentId])
                {
                    ordered.Add(StorageLocationDto.From(location));
                    Visit(location.Id);
                }
            }

            Visit(null);

            return ordered;
        }
    }
}
