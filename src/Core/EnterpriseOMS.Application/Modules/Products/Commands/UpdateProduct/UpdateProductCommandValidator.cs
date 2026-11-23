using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Products.Commands.UpdateProduct
{
    public class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
    {
        public UpdateProductCommandValidator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.Version).NotEmpty().WithMessage("Version is required. Reload the product and try again.");
            RuleFor(x => x.Name).ValidName();
            RuleFor(x => x.Description).ValidDescription();
            RuleFor(x => x.Category).ValidCategory();
            RuleFor(x => x.UnitOfMeasure).ValidUnitOfMeasure();
            RuleFor(x => x.UnitPrice).ValidPrice("Unit price");
            RuleFor(x => x.CostPrice).ValidPrice("Cost price");
            RuleFor(x => x.ReorderLevel).ValidReorderLevel();
        }
    }
}
