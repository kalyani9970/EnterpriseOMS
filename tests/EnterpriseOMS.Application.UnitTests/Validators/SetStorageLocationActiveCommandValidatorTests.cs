using EnterpriseOMS.Application.Modules.Warehouses.Commands.SetStorageLocationActive;
using FluentValidation.TestHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.UnitTests.Validators
{
    public class SetStorageLocationActiveCommandValidatorTests
    {
        private readonly SetStorageLocationActiveCommandValidator _validator = new();

        [Fact]
        public void ValidCommand_HasNoErrors()
        {
            var command = new SetStorageLocationActiveCommand { WarehouseId = 3, Id = 8, Version = Guid.NewGuid(), IsActive = false };

            _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void WarehouseId_Zero_Fails()
        {
            var command = new SetStorageLocationActiveCommand { WarehouseId = 0, Id = 8, Version = Guid.NewGuid() };

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.WarehouseId);
        }

        [Fact]
        public void Id_Zero_Fails()
        {
            var command = new SetStorageLocationActiveCommand { WarehouseId = 3, Id = 0, Version = Guid.NewGuid() };

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Id);
        }

        [Fact]
        public void Version_Missing_Fails()
        {
            var command = new SetStorageLocationActiveCommand { WarehouseId = 3, Id = 8, Version = Guid.Empty };

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Version);
        }
    }
}
