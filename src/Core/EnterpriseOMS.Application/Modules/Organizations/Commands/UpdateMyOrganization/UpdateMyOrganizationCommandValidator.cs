using EnterpriseOMS.Domain.Entities.Tenancy;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Organizations.Commands.UpdateMyOrganization
{
    public class UpdateMyOrganizationCommandValidator : AbstractValidator<UpdateMyOrganizationCommand>
    {
        public UpdateMyOrganizationCommandValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Organization name is required.")
                .MinimumLength(2).WithMessage("Organization name must be at least 2 characters.")
                .MaximumLength(Organization.NameMaxLength);
        }
    }
}
