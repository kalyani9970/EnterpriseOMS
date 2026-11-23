using EnterpriseOMS.Application.Modules.Warehouses.DTOs;
using EnterpriseOMS.Domain.Entities.Warehousing;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Warehouses.Commands.CreateStorageLocation
{
    public class CreateStorageLocationCommand : IRequest<StorageLocationDto>
    {
        public int WarehouseId { get; set; }

        /// <summary>The location to place this one inside, or null for a top-level location.</summary>
        public int? ParentId { get; set; }

        public string Code { get; set; } = default!;
        public string? Name { get; set; }
        public StorageLocationType Type { get; set; }
    }
}
