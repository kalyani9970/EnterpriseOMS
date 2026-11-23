using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Products.Commands.SetProductActive
{
    public class SetProductActiveCommandValidator : AbstractValidator<SetProductActiveCommand>
    {
        public SetProductActiveCommandValidator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.Version).NotEmpty().WithMessage("Version is required. Reload the product and try again.");
        }
    }
}
