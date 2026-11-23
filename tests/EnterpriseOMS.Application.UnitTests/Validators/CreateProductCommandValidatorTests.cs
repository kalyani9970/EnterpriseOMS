using EnterpriseOMS.Application.Modules.Products.Commands.CreateProduct;
using EnterpriseOMS.Domain.Entities.Catalog;
using FluentValidation.TestHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.UnitTests.Validators
{
    public class CreateProductCommandValidatorTests
    {
        private readonly CreateProductCommandValidator _validator = new();

        private static CreateProductCommand ValidCommand() => new()
        {
            Sku = "LAP-001",
            Name = "Business Laptop",
            Description = "14 inch",
            Category = "Electronics",
            UnitOfMeasure = UnitOfMeasure.Piece,
            UnitPrice = 899.00m,
            CostPrice = 690.00m,
            ReorderLevel = 5
        };

        [Fact]
        public void ValidCommand_HasNoErrors()
        {
            _validator.TestValidate(ValidCommand()).ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void OptionalFields_CanBeLeftOut()
        {
            var command = ValidCommand();
            command.Description = null;
            command.Category = null;

            _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
        }

        // ---------- SKU ----------

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Sku_Empty_Fails(string sku)
        {
            var command = ValidCommand();
            command.Sku = sku;

            _validator.TestValidate(command)
                .ShouldHaveValidationErrorFor(x => x.Sku)
                .WithErrorMessage("SKU is required.");
        }

        [Fact]
        public void Sku_Longer_Than50_Fails()
        {
            var command = ValidCommand();
            command.Sku = new string('A', 51);

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Sku);
        }

        [Fact]
        public void Sku_OfExactly50Characters_IsAccepted()
        {
            var command = ValidCommand();
            command.Sku = new string('A', 50);

            _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
        }

        [Theory]
        [InlineData("-abc")]
        [InlineData("a b")]
        [InlineData("a#b")]
        [InlineData("ab*")]
        public void Sku_WithIllegalCharacters_Fails(string sku)
        {
            var command = ValidCommand();
            command.Sku = sku;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Sku);
        }

        [Theory]
        [InlineData("A")]
        [InlineData("a.b-c_d/e")]
        [InlineData("LAP-001")]
        public void Sku_WithAllowedCharacters_IsAccepted(string sku)
        {
            var command = ValidCommand();
            command.Sku = sku;

            _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
        }

        // ---------- Name, description, category ----------

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Name_Empty_Fails(string name)
        {
            var command = ValidCommand();
            command.Name = name;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Name_Longer_Than200_Fails()
        {
            var command = ValidCommand();
            command.Name = new string('x', 201);

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Description_Longer_Than2000_Fails()
        {
            var command = ValidCommand();
            command.Description = new string('x', 2001);

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Description);
        }

        [Fact]
        public void Category_Longer_Than100_Fails()
        {
            var command = ValidCommand();
            command.Category = new string('x', 101);

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Category);
        }

        // ---------- Unit of measure ----------

        [Fact]
        public void UnitOfMeasure_NotInTheEnum_Fails()
        {
            var command = ValidCommand();
            command.UnitOfMeasure = (UnitOfMeasure)99;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.UnitOfMeasure);
        }

        // ---------- Prices ----------

        [Fact]
        public void UnitPrice_Negative_Fails()
        {
            var command = ValidCommand();
            command.UnitPrice = -0.01m;

            _validator.TestValidate(command)
                .ShouldHaveValidationErrorFor(x => x.UnitPrice)
                .WithErrorMessage("Unit price cannot be negative.");
        }

        [Fact]
        public void UnitPrice_WithThreeDecimals_Fails()
        {
            var command = ValidCommand();
            command.UnitPrice = 1.005m;

            _validator.TestValidate(command)
                .ShouldHaveValidationErrorFor(x => x.UnitPrice)
                .WithErrorMessage("Unit price can have at most 2 decimal places.");
        }

        [Fact]
        public void UnitPrice_AboveTheMaximum_Fails()
        {
            var command = ValidCommand();
            command.UnitPrice = 1_000_000_000m;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.UnitPrice);
        }

        [Fact]
        public void UnitPrice_ZeroAndMaximum_AreAccepted()
        {
            var zero = ValidCommand();
            zero.UnitPrice = 0m;
            var maximum = ValidCommand();
            maximum.UnitPrice = Product.MaxPrice;

            _validator.TestValidate(zero).ShouldNotHaveAnyValidationErrors();
            _validator.TestValidate(maximum).ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void CostPrice_Negative_Fails()
        {
            var command = ValidCommand();
            command.CostPrice = -1m;

            _validator.TestValidate(command)
                .ShouldHaveValidationErrorFor(x => x.CostPrice)
                .WithErrorMessage("Cost price cannot be negative.");
        }

        [Fact]
        public void CostPrice_WithThreeDecimals_Fails()
        {
            var command = ValidCommand();
            command.CostPrice = 2.999m;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.CostPrice);
        }

        // ---------- Reorder level ----------

        [Theory]
        [InlineData(-1)]
        [InlineData(1_000_001)]
        public void ReorderLevel_OutOfRange_Fails(int reorderLevel)
        {
            var command = ValidCommand();
            command.ReorderLevel = reorderLevel;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.ReorderLevel);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1_000_000)]
        public void ReorderLevel_AtTheLimits_IsAccepted(int reorderLevel)
        {
            var command = ValidCommand();
            command.ReorderLevel = reorderLevel;

            _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
        }
    }
}
