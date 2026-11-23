using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Warehouses.Commands.UpdateWarehouse
{
    public class UpdateWarehouseCommandValidator : AbstractValidator<UpdateWarehouseCommand>
    {
        public UpdateWarehouseCommandValidator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.Version).NotEmpty().WithMessage("Version is required. Reload the warehouse and try again.");
            RuleFor(x => x.Name).ValidWarehouseName();
            RuleFor(x => x.Address).ValidAddress();
        }
    }
}
