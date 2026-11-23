using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Warehouses.Commands.UpdateStorageLocation
{
    public class UpdateStorageLocationCommandValidator : AbstractValidator<UpdateStorageLocationCommand>
    {
        public UpdateStorageLocationCommandValidator()
        {
            RuleFor(x => x.WarehouseId).GreaterThan(0);
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.Version).NotEmpty().WithMessage("Version is required. Reload the location and try again.");
            RuleFor(x => x.Name).ValidLocationName();
        }
    }
}
