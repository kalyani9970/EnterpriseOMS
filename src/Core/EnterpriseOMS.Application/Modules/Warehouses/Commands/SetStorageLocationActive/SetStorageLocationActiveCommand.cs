using EnterpriseOMS.Application.Modules.Warehouses.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Warehouses.Commands.SetStorageLocationActive
{
    public class SetStorageLocationActiveCommand : IRequest<StorageLocationDto>
    {
        public int WarehouseId { get; set; }
        public int Id { get; set; }
        public Guid Version { get; set; }
        public bool IsActive { get; set; }
    }
}
