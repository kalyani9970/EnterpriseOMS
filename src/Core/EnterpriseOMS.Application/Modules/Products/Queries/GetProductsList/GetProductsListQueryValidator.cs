// Search and Category are optional (string?). null passes the length rules, so the nullability warning is safe to silence.
#pragma warning disable CS8619, CS8620
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Products.Queries.GetProductsList
{
    public class GetProductsListQueryValidator : AbstractValidator<GetProductsListQuery>
    {
        public GetProductsListQueryValidator()
        {
            RuleFor(x => x.PageNumber)
                .GreaterThanOrEqualTo(1).WithMessage("Page number must be 1 or higher.");

            // The cap protects the database: nobody can ask for a million rows in one call.
            RuleFor(x => x.PageSize)
                .GreaterThanOrEqualTo(1).WithMessage("Page size must be at least 1.")
                .LessThanOrEqualTo(GetProductsListQuery.MaxPageSize)
                .WithMessage($"Page size cannot exceed {GetProductsListQuery.MaxPageSize}.");

            RuleFor(x => x.Search)
                .MaximumLength(100).WithMessage("Search text must be 100 characters or fewer.");

            RuleFor(x => x.Category)
                .MaximumLength(100).WithMessage("Category must be 100 characters or fewer.");

            RuleFor(x => x.SortBy)
                .Must(field => GetProductsListQuery.SortFields.Contains(field?.ToLowerInvariant()))
                .WithMessage($"Sort field must be one of: {string.Join(", ", GetProductsListQuery.SortFields)}.");
        }
    }
}
