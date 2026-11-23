using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.Entities.Warehousing
{
    /// <summary>
    /// The levels of a warehouse, from the biggest area down to the smallest. The number IS the level:
    /// a location may only sit inside a location of a LOWER number, so the hierarchy can never loop.
    /// </summary>
    public enum StorageLocationType
    {
        Zone = 1,
        Aisle = 2,
        Rack = 3,
        Shelf = 4,
        Bin = 5
    }
}
