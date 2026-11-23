using EnterpriseOMS.Domain.Entities.Warehousing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Warehouses.DTOs
{
    public class StorageLocationDto
    {
        public int Id { get; set; }
        public int WarehouseId { get; set; }

        /// <summary>The location this one sits inside, or null for a top-level location. Clients build the tree from this.</summary>
        public int? ParentId { get; set; }

        public string Code { get; set; } = default!;
        public string? Name { get; set; }
        public StorageLocationType Type { get; set; }
        public bool IsActive { get; set; }
        public Guid Version { get; set; }
        public DateTime Created { get; set; }
        public DateTime? LastModified { get; set; }

        public static StorageLocationDto From(StorageLocation location) => new()
        {
            Id = location.Id,
            WarehouseId = location.WarehouseId,
            ParentId = location.ParentId,
            Code = location.Code,
            Name = location.Name,
            Type = location.Type,
            IsActive = location.IsActive,
            Version = location.Version,
            Created = location.Created,
            LastModified = location.LastModified
        };
    }
}
