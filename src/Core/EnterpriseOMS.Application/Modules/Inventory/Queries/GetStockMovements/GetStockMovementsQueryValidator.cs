using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Inventory.Queries.GetStockMovements
{
    public class GetStockMovementsQueryValidator : AbstractValidator<GetStockMovementsQuery>
    {
        public GetStockMovementsQueryValidator()
        {
            RuleFor(x => x.PageNumber)
                .GreaterThanOrEqualTo(1).WithMessage("Page number must be 1 or higher.");

            RuleFor(x => x.PageSize)
                .GreaterThanOrEqualTo(1).WithMessage("Page size must be at least 1.")
                .LessThanOrEqualTo(GetStockMovementsQuery.MaxPageSize)
                .WithMessage($"Page size cannot exceed {GetStockMovementsQuery.MaxPageSize}.");

            RuleFor(x => x.Type)
                .IsInEnum().WithMessage("Movement type is not valid.")
                .When(x => x.Type.HasValue);
        }
    }
}
