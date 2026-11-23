using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Warehouses.Commands.SetWarehouseActive
{
    public class SetWarehouseActiveCommandValidator : AbstractValidator<SetWarehouseActiveCommand>
    {
        public SetWarehouseActiveCommandValidator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.Version).NotEmpty().WithMessage("Version is required. Reload the warehouse and try again.");
        }
    }
}
