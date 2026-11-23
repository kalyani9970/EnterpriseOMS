using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Products.Commands.CreateProduct
{
    public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
    {
        public CreateProductCommandValidator()
        {
            RuleFor(x => x.Sku).ValidSku();
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
