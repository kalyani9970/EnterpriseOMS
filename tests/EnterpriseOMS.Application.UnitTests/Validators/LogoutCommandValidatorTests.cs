using EnterpriseOMS.Application.Modules.Auth.Commands.Logout;
using FluentValidation.TestHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.UnitTests.Validators
{
    public class LogoutCommandValidatorTests
    {
        private readonly LogoutCommandValidator _validator = new();

        [Fact]
        public void ValidToken_HasNoErrors()
        {
            var command = new LogoutCommand { RefreshToken = "some-token" };

            _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void EmptyToken_Fails()
        {
            var command = new LogoutCommand { RefreshToken = "" };

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.RefreshToken);
        }

        [Fact]
        public void TooLongToken_Fails()
        {
            var command = new LogoutCommand { RefreshToken = new string('x', 201) };

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.RefreshToken);
        }
    }
}
