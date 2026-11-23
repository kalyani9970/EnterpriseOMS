using EnterpriseOMS.Application.Modules.Products.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Products.Commands.SetProductActive
{
    /// <summary>Deactivates (IsActive = false) or reactivates (true) a product. Products are never deleted.</summary>
    public class SetProductActiveCommand : IRequest<ProductDto>
    {
        public int Id { get; set; }
        public Guid Version { get; set; }
        public bool IsActive { get; set; }
    }
}
