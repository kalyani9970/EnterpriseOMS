using EnterpriseOMS.Application.Modules.Auth.Commands.RefreshTokens;
using FluentValidation.TestHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.UnitTests.Validators
{
    public class RefreshTokenCommandValidatorTests
    {
        private readonly RefreshTokenCommandValidator _validator = new();

        [Fact]
        public void ValidToken_HasNoErrors()
        {
            var command = new RefreshTokenCommand { RefreshToken = "some-token" };

            _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void EmptyToken_Fails()
        {
            var command = new RefreshTokenCommand { RefreshToken = "" };

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.RefreshToken);
        }

        [Fact]
        public void TooLongToken_Fails()
        {
            var command = new RefreshTokenCommand { RefreshToken = new string('x', 201) };

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.RefreshToken);
        }
    }
}
