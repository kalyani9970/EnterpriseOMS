using EnterpriseOMS.Application.Modules.Products.Commands.UpdateProduct;
using EnterpriseOMS.Domain.Entities.Catalog;
using FluentValidation.TestHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.UnitTests.Validators
{
    public class UpdateProductCommandValidatorTests
    {
        private readonly UpdateProductCommandValidator _validator = new();

        private static UpdateProductCommand ValidCommand() => new()
        {
            Id = 3,
            Version = Guid.NewGuid(),
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

        [Theory]
        [InlineData(0)]
        [InlineData(-4)]
        public void Id_NotPositive_Fails(int id)
        {
            var command = ValidCommand();
            command.Id = id;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Id);
        }

        [Fact]
        public void Version_Missing_Fails_BecauseAnEditMustProveWhatItWasBasedOn()
        {
            var command = ValidCommand();
            command.Version = Guid.Empty;

            _validator.TestValidate(command)
                .ShouldHaveValidationErrorFor(x => x.Version)
                .WithErrorMessage("Version is required. Reload the product and try again.");
        }

        [Fact]
        public void Name_Empty_Fails()
        {
            var command = ValidCommand();
            command.Name = "   ";

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void UnitPrice_WithThreeDecimals_Fails()
        {
            var command = ValidCommand();
            command.UnitPrice = 1.005m;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.UnitPrice);
        }

        [Fact]
        public void CostPrice_Negative_Fails()
        {
            var command = ValidCommand();
            command.CostPrice = -1m;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.CostPrice);
        }

        [Fact]
        public void UnitOfMeasure_NotInTheEnum_Fails()
        {
            var command = ValidCommand();
            command.UnitOfMeasure = (UnitOfMeasure)0;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.UnitOfMeasure);
        }

        [Fact]
        public void ReorderLevel_Negative_Fails()
        {
            var command = ValidCommand();
            command.ReorderLevel = -1;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.ReorderLevel);
        }

        [Fact]
        public void Description_Longer_Than2000_Fails()
        {
            var command = ValidCommand();
            command.Description = new string('x', 2001);

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Description);
        }
    }
}
