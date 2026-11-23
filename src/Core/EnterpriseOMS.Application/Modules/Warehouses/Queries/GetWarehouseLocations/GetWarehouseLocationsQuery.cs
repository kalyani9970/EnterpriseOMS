using EnterpriseOMS.Application.Modules.Warehouses.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Warehouses.Queries.GetWarehouseLocations
{
    /// <summary>Every location of one warehouse, in tree order (each parent directly followed by its descendants).</summary>
    public class GetWarehouseLocationsQuery : IRequest<List<StorageLocationDto>>
    {
        public int WarehouseId { get; set; }

        /// <summary>true = leave out inactive locations (for pickers); false = show everything (for management).</summary>
        public bool ActiveOnly { get; set; }
    }
}
