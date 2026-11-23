using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Warehouses.Commands.CreateStorageLocation
{
    public class CreateStorageLocationCommandValidator : AbstractValidator<CreateStorageLocationCommand>
    {
        public CreateStorageLocationCommandValidator()
        {
            RuleFor(x => x.WarehouseId).GreaterThan(0);
            RuleFor(x => x.Code).ValidLocationCode();
            RuleFor(x => x.Name).ValidLocationName();
            RuleFor(x => x.Type).ValidLocationType();
        }
    }
}
