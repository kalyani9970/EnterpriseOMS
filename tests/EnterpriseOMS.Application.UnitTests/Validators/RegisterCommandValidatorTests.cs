using EnterpriseOMS.Application.Modules.Auth.Commands.Register;
using FluentValidation.TestHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.UnitTests.Validators
{
    public class RegisterCommandValidatorTests
    {
        private readonly RegisterCommandValidator _validator = new();

        private static RegisterCommand ValidCommand() => new()
        {
            FullName = "Jane Doe",
            Email = "jane@example.com",
            Password = "Passw0rd1",
            OrganizationId = 1
        };

        [Fact]
        public void ValidCommand_HasNoErrors()
        {
            var result = _validator.TestValidate(ValidCommand());

            result.ShouldNotHaveAnyValidationErrors();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void FullName_Empty_Fails(string fullName)
        {
            var command = ValidCommand();
            command.FullName = fullName;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.FullName);
        }

        [Fact]
        public void FullName_TooLong_Fails()
        {
            var command = ValidCommand();
            command.FullName = new string('a', 201);

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.FullName);
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-an-email")]
        [InlineData("missing-domain@")]
        public void Email_Invalid_Fails(string email)
        {
            var command = ValidCommand();
            command.Email = email;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Email);
        }

        [Fact]
        public void Email_TooLong_Fails()
        {
            var command = ValidCommand();
            command.Email = new string('a', 250) + "@example.com";

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Email);
        }

        [Theory]
        [InlineData("")]                // empty
        [InlineData("Short1A")]         // 7 characters
        [InlineData("alllowercase1")]   // no uppercase
        [InlineData("ALLUPPERCASE1")]   // no lowercase
        [InlineData("NoDigitsHere")]    // no digit
        public void Password_BreakingAnyRule_Fails(string password)
        {
            var command = ValidCommand();
            command.Password = password;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Password);
        }

        [Fact]
        public void Password_MissingUppercase_ReportsSpecificMessage()
        {
            var command = ValidCommand();
            command.Password = "alllowercase1";

            _validator.TestValidate(command)
                .ShouldHaveValidationErrorFor(x => x.Password)
                .WithErrorMessage("Password must contain at least one uppercase letter.");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void OrganizationId_NotPositive_Fails(int organizationId)
        {
            var command = ValidCommand();
            command.OrganizationId = organizationId;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.OrganizationId);
        }
    }
}
