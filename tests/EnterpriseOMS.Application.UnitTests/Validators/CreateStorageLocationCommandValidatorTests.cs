using EnterpriseOMS.Application.Modules.Warehouses.Commands.CreateStorageLocation;
using EnterpriseOMS.Domain.Entities.Warehousing;
using FluentValidation.TestHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.UnitTests.Validators
{
    public class CreateStorageLocationCommandValidatorTests
    {
        private readonly CreateStorageLocationCommandValidator _validator = new();

        private static CreateStorageLocationCommand ValidCommand() => new()
        {
            WarehouseId = 3,
            ParentId = null,
            Code = "A-01",
            Name = "Aisle one",
            Type = StorageLocationType.Aisle
        };

        [Fact]
        public void ValidCommand_HasNoErrors()
        {
            _validator.TestValidate(ValidCommand()).ShouldNotHaveAnyValidationErrors();
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
        [InlineData("")]
        [InlineData("   ")]
        public void Code_Empty_Fails(string code)
        {
            var command = ValidCommand();
            command.Code = code;

            _validator.TestValidate(command)
                .ShouldHaveValidationErrorFor(x => x.Code)
                .WithErrorMessage("Location code is required.");
        }

        [Fact]
        public void Code_LongerThan30_Fails()
        {
            var command = ValidCommand();
            command.Code = new string('A', 31);

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Code);
        }

        [Fact]
        public void Code_OfExactly30Characters_IsAccepted()
        {
            var command = ValidCommand();
            command.Code = new string('A', 30);

            _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
        }

        [Theory]
        [InlineData("A B")]
        [InlineData("-A")]
        [InlineData("A#1")]
        public void Code_WithIllegalCharacters_Fails(string code)
        {
            var command = ValidCommand();
            command.Code = code;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Code);
        }

        [Fact]
        public void Name_CanBeLeftOut()
        {
            var command = ValidCommand();
            command.Name = null;

            _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Name_LongerThan200_Fails()
        {
            var command = ValidCommand();
            command.Name = new string('x', 201);

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Name_OfExactly200Characters_IsAccepted()
        {
            var command = ValidCommand();
            command.Name = new string('x', 200);

            _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(99)]
        public void Type_NotInTheEnum_Fails(int type)
        {
            var command = ValidCommand();
            command.Type = (StorageLocationType)type;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Type);
        }
    }
}
