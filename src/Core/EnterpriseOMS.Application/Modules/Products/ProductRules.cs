// FluentValidation's string rules are declared for non-nullable string; our optional text fields are string?.
// The rules behave identically for null (null passes), so the nullability warning is safe to silence here.
#pragma warning disable CS8619, CS8620
using EnterpriseOMS.Domain.Entities.Catalog;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Products
{
    /// <summary>
    /// The product field rules, written once and reused by the create and update validators (DRY).
    /// The domain entity re-checks the same rules, so it can never hold invalid data even if a
    /// validator were missed. Validators exist to give the caller friendly, field-level messages.
    /// </summary>
    public static class ProductRules
    {
        public static IRuleBuilderOptions<T, string> ValidSku<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty().WithMessage("SKU is required.")
        .MaximumLength(Product.SkuMaxLength).WithMessage($"SKU must be {Product.SkuMaxLength} characters or fewer.")
        .Matches(@"^[A-Za-z0-9][A-Za-z0-9._/\-]*$")
        .WithMessage("SKU may contain only letters, digits, dots, hyphens, underscores and slashes, and must start with a letter or digit.");

        public static IRuleBuilderOptions<T, string> ValidName<T>(this IRuleBuilder<T, string> rule) => rule
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(Product.NameMaxLength).WithMessage($"Product name must be {Product.NameMaxLength} characters or fewer.");

        public static IRuleBuilderOptions<T, string?> ValidDescription<T>(this IRuleBuilder<T, string?> rule) => rule
            .MaximumLength(Product.DescriptionMaxLength).WithMessage($"Description must be {Product.DescriptionMaxLength} characters or fewer.");

        public static IRuleBuilderOptions<T, string?> ValidCategory<T>(this IRuleBuilder<T, string?> rule) => rule
            .MaximumLength(Product.CategoryMaxLength).WithMessage($"Category must be {Product.CategoryMaxLength} characters or fewer.");

        public static IRuleBuilderOptions<T, UnitOfMeasure> ValidUnitOfMeasure<T>(this IRuleBuilder<T, UnitOfMeasure> rule) => rule
            .IsInEnum().WithMessage("Unit of measure is not valid.");

        public static IRuleBuilderOptions<T, decimal> ValidPrice<T>(this IRuleBuilder<T, decimal> rule, string label) => rule
            .GreaterThanOrEqualTo(0m).WithMessage($"{label} cannot be negative.")
            .LessThanOrEqualTo(Product.MaxPrice).WithMessage($"{label} cannot exceed 999,999,999.99.")
            .Must(price => decimal.Round(price, 2) == price).WithMessage($"{label} can have at most 2 decimal places.");

        public static IRuleBuilderOptions<T, int> ValidReorderLevel<T>(this IRuleBuilder<T, int> rule) => rule
            .GreaterThanOrEqualTo(0).WithMessage("Reorder level cannot be negative.")
            .LessThanOrEqualTo(Product.MaxReorderLevel).WithMessage($"Reorder level cannot exceed {Product.MaxReorderLevel:N0}.");
    }
}
