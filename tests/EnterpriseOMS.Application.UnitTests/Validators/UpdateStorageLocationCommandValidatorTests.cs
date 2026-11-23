using EnterpriseOMS.Application.Modules.Warehouses.Commands.UpdateStorageLocation;
using FluentValidation.TestHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.UnitTests.Validators
{
    public class UpdateStorageLocationCommandValidatorTests
    {
        private readonly UpdateStorageLocationCommandValidator _validator = new();

        private static UpdateStorageLocationCommand ValidCommand() => new()
        {
            WarehouseId = 3,
            Id = 8,
            Version = Guid.NewGuid(),
            Name = "Aisle one"
        };

        [Fact]
        public void ValidCommand_HasNoErrors()
        {
            _validator.TestValidate(ValidCommand()).ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void WarehouseId_Zero_Fails()
        {
            var command = ValidCommand();
            command.WarehouseId = 0;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.WarehouseId);
        }

        [Fact]
        public void Id_Zero_Fails()
        {
            var command = ValidCommand();
            command.Id = 0;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Id);
        }

        [Fact]
        public void Version_Missing_Fails()
        {
            var command = ValidCommand();
            command.Version = Guid.Empty;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Version);
        }

        [Fact]
        public void Name_CanBeCleared()
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
    }
}
