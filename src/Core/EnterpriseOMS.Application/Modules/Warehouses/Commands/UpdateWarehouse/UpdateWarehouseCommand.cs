using EnterpriseOMS.Application.Modules.Warehouses.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Warehouses.Commands.UpdateWarehouse
{
    /// <summary>There is no Code here on purpose: a warehouse code never changes after creation.</summary>
    public class UpdateWarehouseCommand : IRequest<WarehouseDto>
    {
        public int Id { get; set; }

        /// <summary>The Version the caller last saw. It proves the edit is based on the current data.</summary>
        public Guid Version { get; set; }

        public string Name { get; set; } = default!;
        public AddressDto Address { get; set; } = default!;
    }
}
