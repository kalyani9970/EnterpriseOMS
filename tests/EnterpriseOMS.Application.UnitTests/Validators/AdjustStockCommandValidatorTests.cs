using EnterpriseOMS.Application.Modules.Inventory.Commands.AdjustStock;
using FluentValidation.TestHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.UnitTests.Validators
{
    public class AdjustStockCommandValidatorTests
    {
        private readonly AdjustStockCommandValidator _validator = new();

        private static AdjustStockCommand ValidCommand() => new()
        {
            ProductId = 3,
            WarehouseId = 10,
            CountedQuantity = 12,
            Reason = "Cycle count"
        };

        [Fact]
        public void ValidCommand_HasNoErrors()
        {
            _validator.TestValidate(ValidCommand()).ShouldNotHaveAnyValidationErrors();
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

        [Fact]
        public void CountedQuantity_Negative_Fails()
        {
            var command = ValidCommand();
            command.CountedQuantity = -1;

            _validator.TestValidate(command)
                .ShouldHaveValidationErrorFor(x => x.CountedQuantity)
                .WithErrorMessage("The counted quantity cannot be negative.");
        }

        [Fact]
        public void CountedQuantity_Zero_IsAccepted_BecauseAnEmptyShelfIsAValidCount()
        {
            var command = ValidCommand();
            command.CountedQuantity = 0;

            _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void CountedQuantity_AboveTheCeiling_Fails()
        {
            var command = ValidCommand();
            command.CountedQuantity = 1_000_000_001;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.CountedQuantity);
        }

        [Fact]
        public void CountedQuantity_AtTheCeiling_IsAccepted()
        {
            var command = ValidCommand();
            command.CountedQuantity = 1_000_000_000;

            _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Reason_Missing_Fails_BecauseAnUnexplainedCorrectionDefeatsTheAuditTrail(string reason)
        {
            var command = ValidCommand();
            command.Reason = reason;

            _validator.TestValidate(command)
                .ShouldHaveValidationErrorFor(x => x.Reason)
                .WithErrorMessage("A reason is required for a stock adjustment.");
        }

        [Fact]
        public void Reason_LongerThan500_Fails()
        {
            var command = ValidCommand();
            command.Reason = new string('x', 501);

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Reason);
        }

        [Fact]
        public void Reason_OfExactly500Characters_IsAccepted()
        {
            var command = ValidCommand();
            command.Reason = new string('x', 500);

            _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
        }
    }
}
