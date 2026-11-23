using EnterpriseOMS.Application.Modules.Warehouses.Commands.SetWarehouseActive;
using FluentValidation.TestHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.UnitTests.Validators
{
    public class SetWarehouseActiveCommandValidatorTests
    {
        private readonly SetWarehouseActiveCommandValidator _validator = new();

        [Fact]
        public void ValidCommand_HasNoErrors()
        {
            var command = new SetWarehouseActiveCommand { Id = 3, Version = Guid.NewGuid(), IsActive = false };

            _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Id_Zero_Fails()
        {
            var command = new SetWarehouseActiveCommand { Id = 0, Version = Guid.NewGuid() };

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Id);
        }

        [Fact]
        public void Version_Missing_Fails()
        {
            var command = new SetWarehouseActiveCommand { Id = 3, Version = Guid.Empty };

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Version);
        }
    }
}
