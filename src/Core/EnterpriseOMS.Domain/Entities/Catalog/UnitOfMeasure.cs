using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.Entities.Catalog
{
    /// <summary>How a product is counted, stocked and priced. Stored as text in the database so the rows stay readable.</summary>
    public enum UnitOfMeasure
    {
        Piece = 1,
        Kilogram = 2,
        Litre = 3,
        Metre = 4,
        Box = 5,
        Pack = 6,
        Pallet = 7
    }
}
