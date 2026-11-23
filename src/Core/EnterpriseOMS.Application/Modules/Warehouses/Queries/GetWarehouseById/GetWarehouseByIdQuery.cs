using EnterpriseOMS.Application.Modules.Warehouses.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Warehouses.Queries.GetWarehouseById
{
    public class GetWarehouseByIdQuery : IRequest<WarehouseDto>
    {
        public int Id { get; set; }
    }
}
