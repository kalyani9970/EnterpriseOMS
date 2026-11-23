using EnterpriseOMS.Application.Modules.Inventory.Commands.IssueStock;
using FluentValidation.TestHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.UnitTests.Validators
{
    public class IssueStockCommandValidatorTests
    {
        private readonly IssueStockCommandValidator _validator = new();

        private static IssueStockCommand ValidCommand() => new()
        {
            ProductId = 3,
            WarehouseId = 10,
            Quantity = 4,
            Reference = "SO-1001",
            Notes = "Shipped to a customer"
        };

        [Fact]
        public void ValidCommand_HasNoErrors()
        {
            _validator.TestValidate(ValidCommand()).ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void ReferenceAndNotes_CanBeLeftOut()
        {
            var command = ValidCommand();
            command.Reference = null;
            command.Notes = null;

            _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void ProductId_NotPositive_Fails(int productId)
        {
            var command = ValidCommand();
            command.ProductId = productId;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.ProductId);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void WarehouseId_NotPositive_Fails(int warehouseId)
        {
            var command = ValidCommand();
            command.WarehouseId = warehouseId;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.WarehouseId);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void Quantity_NotPositive_Fails(int quantity)
        {
            var command = ValidCommand();
            command.Quantity = quantity;

            _validator.TestValidate(command)
                .ShouldHaveValidationErrorFor(x => x.Quantity)
                .WithErrorMessage("Quantity must be greater than zero.");
        }

        [Fact]
        public void Quantity_AboveTheCeiling_Fails()
        {
            var command = ValidCommand();
            command.Quantity = 1_000_000_001;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Quantity);
        }

        [Fact]
        public void Reference_LongerThan100_Fails()
        {
            var command = ValidCommand();
            command.Reference = new string('x', 101);

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Reference);
        }

        [Fact]
        public void Notes_LongerThan500_Fails()
        {
            var command = ValidCommand();
            command.Notes = new string('x', 501);

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Notes);
        }
    }
}
