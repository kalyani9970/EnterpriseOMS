using EnterpriseOMS.Application.Modules.Inventory.Commands.ReceiveStock;
using FluentValidation.TestHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.UnitTests.Validators
{
    public class ReceiveStockCommandValidatorTests
    {
        private readonly ReceiveStockCommandValidator _validator = new();

        private static ReceiveStockCommand ValidCommand() => new()
        {
            ProductId = 3,
            WarehouseId = 10,
            Quantity = 25,
            Reference = "PO-5001",
            Notes = "Delivery from the main supplier"
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

        // ---------- product and warehouse ----------

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void ProductId_NotPositive_Fails(int productId)
        {
            var command = ValidCommand();
            command.ProductId = productId;

            _validator.TestValidate(command)
                .ShouldHaveValidationErrorFor(x => x.ProductId)
                .WithErrorMessage("Product is required.");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void WarehouseId_NotPositive_Fails(int warehouseId)
        {
            var command = ValidCommand();
            command.WarehouseId = warehouseId;

            _validator.TestValidate(command)
                .ShouldHaveValidationErrorFor(x => x.WarehouseId)
                .WithErrorMessage("Warehouse is required.");
        }

        // ---------- quantity ----------

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

        [Theory]
        [InlineData(1)]
        [InlineData(1_000_000_000)]
        public void Quantity_AtTheLimits_IsAccepted(int quantity)
        {
            var command = ValidCommand();
            command.Quantity = quantity;

            _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
        }

        // ---------- reference and notes ----------

        [Fact]
        public void Reference_LongerThan100_Fails()
        {
            var command = ValidCommand();
            command.Reference = new string('x', 101);

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Reference);
        }

        [Fact]
        public void Reference_OfExactly100Characters_IsAccepted()
        {
            var command = ValidCommand();
            command.Reference = new string('x', 100);

            _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Notes_LongerThan500_Fails()
        {
            var command = ValidCommand();
            command.Notes = new string('x', 501);

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Notes);
        }

        [Fact]
        public void Notes_OfExactly500Characters_IsAccepted()
        {
            var command = ValidCommand();
            command.Notes = new string('x', 500);

            _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
        }
    }
}
