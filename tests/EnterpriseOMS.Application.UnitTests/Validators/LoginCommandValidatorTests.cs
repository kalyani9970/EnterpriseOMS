using EnterpriseOMS.Application.Modules.Auth.Commands.Login;
using FluentValidation.TestHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.UnitTests.Validators
{
    public class LoginCommandValidatorTests
    {
        private readonly LoginCommandValidator _validator = new();

        [Fact]
        public void ValidCommand_HasNoErrors()
        {
            var command = new LoginCommand { Email = "jane@example.com", Password = "anything" };

            _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-an-email")]
        public void Email_Invalid_Fails(string email)
        {
            var command = new LoginCommand { Email = email, Password = "anything" };

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Email);
        }

        [Fact]
        public void Password_Empty_Fails()
        {
            var command = new LoginCommand { Email = "jane@example.com", Password = "" };

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Password);
        }
    }
}
