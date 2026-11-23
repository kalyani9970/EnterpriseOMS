using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Warehouses.DTOs
{
    /// <summary>The slim shape used in lists: only what a table row needs.</summary>
    public class WarehouseListItemDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string City { get; set; } = default!;
        public string CountryCode { get; set; } = default!;
        public bool IsActive { get; set; }
        public Guid Version { get; set; }
        public int LocationCount { get; set; }
    }
}
