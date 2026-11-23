using EnterpriseOMS.Application.Modules.Warehouses.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Warehouses.Commands.UpdateStorageLocation
{
    /// <summary>
    /// Only the name can change. A location's code, type and parent are fixed once it exists:
    /// stock will be recorded against it, and moving it around would rewrite history.
    /// </summary>
    public class UpdateStorageLocationCommand : IRequest<StorageLocationDto>
    {
        public int WarehouseId { get; set; }
        public int Id { get; set; }
        public Guid Version { get; set; }
        public string? Name { get; set; }
    }
}
