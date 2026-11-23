// FluentValidation's string rules are declared for non-nullable string; our optional text fields are string?.
// The rules behave identically for null (null passes), so the nullability warning is safe to silence here.
#pragma warning disable CS8619, CS8620
using EnterpriseOMS.Domain.Entities.Inventory;
using FluentValidation;

namespace EnterpriseOMS.Application.Modules.Inventory;

/// <summary>
/// The stock field rules, written once and reused by every validator (DRY). The domain entities re-check
/// the same rules, so they can never hold invalid data even if a validator were missed. Validators exist to
/// give the caller friendly, field-level messages.
/// </summary>
public static class InventoryRules
{
    public static IRuleBuilderOptions<T, int> ValidProductId<T>(this IRuleBuilder<T, int> rule) => rule
        .GreaterThan(0).WithMessage("Product is required.");

    public static IRuleBuilderOptions<T, int> ValidWarehouseId<T>(this IRuleBuilder<T, int> rule) => rule
        .GreaterThan(0).WithMessage("Warehouse is required.");

    /// <summary>The quantity of a receipt or an issue: a positive whole number.</summary>
    public static IRuleBuilderOptions<T, int> ValidQuantity<T>(this IRuleBuilder<T, int> rule) => rule
        .GreaterThan(0).WithMessage("Quantity must be greater than zero.")
        .LessThanOrEqualTo(StockLevel.MaxQuantity).WithMessage("Quantity cannot exceed 1,000,000,000.");

    /// <summary>The number found in a stock count: zero is a valid count.</summary>
    public static IRuleBuilderOptions<T, int> ValidCountedQuantity<T>(this IRuleBuilder<T, int> rule) => rule
        .GreaterThanOrEqualTo(0).WithMessage("The counted quantity cannot be negative.")
        .LessThanOrEqualTo(StockLevel.MaxQuantity).WithMessage("The counted quantity cannot exceed 1,000,000,000.");

    public static IRuleBuilderOptions<T, string?> ValidReference<T>(this IRuleBuilder<T, string?> rule) => rule
        .MaximumLength(StockMovement.ReferenceMaxLength)
        .WithMessage($"Reference must be {StockMovement.ReferenceMaxLength} characters or fewer.");

    public static IRuleBuilderOptions<T, string?> ValidNotes<T>(this IRuleBuilder<T, string?> rule) => rule
        .MaximumLength(StockMovement.NotesMaxLength)
        .WithMessage($"Notes must be {StockMovement.NotesMaxLength} characters or fewer.");
}