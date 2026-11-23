// Search is optional (string?). null passes the length rule, so the nullability warning is safe to silence.
#pragma warning disable CS8619, CS8620
using FluentValidation;

namespace EnterpriseOMS.Application.Modules.Warehouses.Queries.GetWarehousesList;

public class GetWarehousesListQueryValidator : AbstractValidator<GetWarehousesListQuery>
{
    public GetWarehousesListQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be 1 or higher.");

        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1).WithMessage("Page size must be at least 1.")
            .LessThanOrEqualTo(GetWarehousesListQuery.MaxPageSize)
            .WithMessage($"Page size cannot exceed {GetWarehousesListQuery.MaxPageSize}.");

        RuleFor(x => x.Search)
            .MaximumLength(100).WithMessage("Search text must be 100 characters or fewer.");

        RuleFor(x => x.SortBy)
            .Must(field => GetWarehousesListQuery.SortFields.Contains(field?.ToLowerInvariant()))
            .WithMessage($"Sort field must be one of: {string.Join(", ", GetWarehousesListQuery.SortFields)}.");
    }
}