using EnterpriseOMS.Application.Modules.Warehouses.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Warehouses.Commands.SetWarehouseActive
{
    /// <summary>Deactivates (IsActive = false) or reactivates (true) a warehouse. Warehouses are never deleted.</summary>
    public class SetWarehouseActiveCommand : IRequest<WarehouseDto>
    {
        public int Id { get; set; }
        public Guid Version { get; set; }
        public bool IsActive { get; set; }
    }
}
