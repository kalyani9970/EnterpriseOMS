using EnterpriseOMS.Application.Modules.Organizations.Commands.UpdateMyOrganization;
using FluentValidation.TestHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.UnitTests.Validators
{
    public class UpdateMyOrganizationCommandValidatorTests
    {
        private readonly UpdateMyOrganizationCommandValidator _validator = new();

        [Fact]
        public void ValidName_HasNoErrors()
        {
            var command = new UpdateMyOrganizationCommand { Name = "Acme Holdings" };

            _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void EmptyName_Fails(string name)
        {
            var command = new UpdateMyOrganizationCommand { Name = name };

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void SingleCharacterName_Fails()
        {
            var command = new UpdateMyOrganizationCommand { Name = "A" };

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void NameLongerThan200_Fails()
        {
            var command = new UpdateMyOrganizationCommand { Name = new string('a', 201) };

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void NameOf200Characters_IsAccepted()
        {
            var command = new UpdateMyOrganizationCommand { Name = new string('a', 200) };

            _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
        }
    }
}
