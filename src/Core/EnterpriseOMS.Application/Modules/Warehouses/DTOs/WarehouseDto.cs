using EnterpriseOMS.Domain.Entities.Warehousing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Warehouses.DTOs
{
    public class WarehouseDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = default!;
        public string Name { get; set; } = default!;
        public AddressDto Address { get; set; } = default!;
        public bool IsActive { get; set; }

        /// <summary>Send this back when updating. If it no longer matches, someone else changed the warehouse first.</summary>
        public Guid Version { get; set; }

        public int LocationCount { get; set; }
        public DateTime Created { get; set; }
        public DateTime? LastModified { get; set; }

        public static WarehouseDto From(Warehouse warehouse, int locationCount) => new()
        {
            Id = warehouse.Id,
            Code = warehouse.Code,
            Name = warehouse.Name,
            Address = AddressDto.From(warehouse.Address),
            IsActive = warehouse.IsActive,
            Version = warehouse.Version,
            LocationCount = locationCount,
            Created = warehouse.Created,
            LastModified = warehouse.LastModified
        };
    }
}
