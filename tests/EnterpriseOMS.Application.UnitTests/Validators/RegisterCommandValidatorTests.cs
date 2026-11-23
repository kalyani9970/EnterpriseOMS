using EnterpriseOMS.Application.Modules.Auth.Commands.Register;
using FluentValidation.TestHelper;

namespace EnterpriseOMS.Application.UnitTests.Validators;

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    private static RegisterCommand ValidCommand() => new()
    {
        FullName = "Jane Doe",
        Email = "jane@example.com",
        Password = "Passw0rd1",
        OrganizationName = "Acme Traders"
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
    [InlineData("")]
    [InlineData("   ")]
    public void OrganizationName_Empty_Fails(string organizationName)
    {
        var command = ValidCommand();
        command.OrganizationName = organizationName;

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.OrganizationName);
    }

    [Fact]
    public void OrganizationName_SingleCharacter_Fails()
    {
        var command = ValidCommand();
        command.OrganizationName = "A";

        _validator.TestValidate(command)
            .ShouldHaveValidationErrorFor(x => x.OrganizationName)
            .WithErrorMessage("Organization name must be at least 2 characters.");
    }

    [Fact]
    public void OrganizationName_LongerThan200_Fails()
    {
        var command = ValidCommand();
        command.OrganizationName = new string('a', 201);

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.OrganizationName);
    }

    [Fact]
    public void OrganizationName_Of200Characters_IsAccepted()
    {
        var command = ValidCommand();
        command.OrganizationName = new string('a', 200);

        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }
}