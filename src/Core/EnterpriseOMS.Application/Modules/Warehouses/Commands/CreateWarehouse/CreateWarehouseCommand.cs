using EnterpriseOMS.Application.Modules.Warehouses.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Warehouses.Commands.CreateWarehouse
{
    public class CreateWarehouseCommand : IRequest<WarehouseDto>
    {
        public string Code { get; set; } = default!;
        public string Name { get; set; } = default!;
        public AddressDto Address { get; set; } = default!;
    }
}
