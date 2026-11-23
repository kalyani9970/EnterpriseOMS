// FluentValidation's string rules are declared for non-nullable string; our optional text fields are string?.
// The rules behave identically for null (null passes), so the nullability warning is safe to silence here.
#pragma warning disable CS8619, CS8620
using EnterpriseOMS.Application.Modules.Warehouses.DTOs;
using EnterpriseOMS.Domain.Entities.Warehousing;
using EnterpriseOMS.Domain.ValueObjects;
using FluentValidation;

namespace EnterpriseOMS.Application.Modules.Warehouses;

/// <summary>
/// The warehouse field rules, written once and reused by every validator (DRY). The domain entities
/// re-check the same rules, so they can never hold invalid data even if a validator were missed.
/// Validators exist to give the caller friendly, field-level messages.
/// </summary>
public static class WarehouseRules
{
    private const string CodeCharacters = @"^[A-Za-z0-9][A-Za-z0-9._/\-]*$";
    private const string CodeCharactersMessage =
        "may contain only letters, digits, dots, hyphens, underscores and slashes, and must start with a letter or digit.";

    public static IRuleBuilderOptions<T, string> ValidWarehouseCode<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty().WithMessage("Warehouse code is required.")
        .MaximumLength(Warehouse.CodeMaxLength).WithMessage($"Warehouse code must be {Warehouse.CodeMaxLength} characters or fewer.")
        .Matches(CodeCharacters).WithMessage($"Warehouse code {CodeCharactersMessage}");

    public static IRuleBuilderOptions<T, string> ValidWarehouseName<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty().WithMessage("Warehouse name is required.")
        .MaximumLength(Warehouse.NameMaxLength).WithMessage($"Warehouse name must be {Warehouse.NameMaxLength} characters or fewer.");

    public static IRuleBuilderOptions<T, string> ValidLocationCode<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty().WithMessage("Location code is required.")
        .MaximumLength(StorageLocation.CodeMaxLength).WithMessage($"Location code must be {StorageLocation.CodeMaxLength} characters or fewer.")
        .Matches(CodeCharacters).WithMessage($"Location code {CodeCharactersMessage}");

    public static IRuleBuilderOptions<T, string?> ValidLocationName<T>(this IRuleBuilder<T, string?> rule) => rule
        .MaximumLength(StorageLocation.NameMaxLength).WithMessage($"Location name must be {StorageLocation.NameMaxLength} characters or fewer.");

    public static IRuleBuilderOptions<T, StorageLocationType> ValidLocationType<T>(this IRuleBuilder<T, StorageLocationType> rule) => rule
        .IsInEnum().WithMessage("Location type is not valid.");

    /// <summary>Validates the nested address. Its errors are reported as "Address.Line1", "Address.City"...</summary>
    public static IRuleBuilderOptions<T, AddressDto> ValidAddress<T>(this IRuleBuilder<T, AddressDto> rule) => rule
        .NotNull().WithMessage("Address is required.")
        .SetValidator(new AddressDtoValidator());
}

public class AddressDtoValidator : AbstractValidator<AddressDto>
{
    public AddressDtoValidator()
    {
        RuleFor(x => x.Line1)
            .NotEmpty().WithMessage("Address line 1 is required.")
            .MaximumLength(Address.LineMaxLength).WithMessage($"Address line 1 must be {Address.LineMaxLength} characters or fewer.");

        RuleFor(x => x.Line2)
            .MaximumLength(Address.LineMaxLength).WithMessage($"Address line 2 must be {Address.LineMaxLength} characters or fewer.");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("City is required.")
            .MaximumLength(Address.CityMaxLength).WithMessage($"City must be {Address.CityMaxLength} characters or fewer.");

        RuleFor(x => x.Region)
            .MaximumLength(Address.RegionMaxLength).WithMessage($"Region must be {Address.RegionMaxLength} characters or fewer.");

        RuleFor(x => x.PostalCode)
            .MaximumLength(Address.PostalCodeMaxLength).WithMessage($"Postal code must be {Address.PostalCodeMaxLength} characters or fewer.");

        RuleFor(x => x.CountryCode)
            .NotEmpty().WithMessage("Country code is required.")
            .Matches("^[A-Za-z]{2}$").WithMessage("Country code must be a 2-letter ISO code such as IN or NL.");
    }
}