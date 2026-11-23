using EnterpriseOMS.Application.Modules.Warehouses.Commands.CreateWarehouse;
using EnterpriseOMS.Application.Modules.Warehouses.DTOs;
using FluentValidation.TestHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.UnitTests.Validators
{
    public class CreateWarehouseCommandValidatorTests
    {
        private readonly CreateWarehouseCommandValidator _validator = new();

        private static CreateWarehouseCommand ValidCommand() => new()
        {
            Code = "WH-MAIN",
            Name = "Main Distribution Center",
            Address = new AddressDto
            {
                Line1 = "12 Industrial Road",
                City = "Pune",
                CountryCode = "IN"
            }
        };

        [Fact]
        public void ValidCommand_HasNoErrors()
        {
            _validator.TestValidate(ValidCommand()).ShouldNotHaveAnyValidationErrors();
        }

        // ---------- code ----------

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Code_Empty_Fails(string code)
        {
            var command = ValidCommand();
            command.Code = code;

            _validator.TestValidate(command)
                .ShouldHaveValidationErrorFor(x => x.Code)
                .WithErrorMessage("Warehouse code is required.");
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
        [InlineData("-x")]
        [InlineData("a b")]
        [InlineData("a#b")]
        public void Code_WithIllegalCharacters_Fails(string code)
        {
            var command = ValidCommand();
            command.Code = code;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Code);
        }

        [Theory]
        [InlineData("A")]
        [InlineData("wh-main")]
        [InlineData("W.1/2")]
        public void Code_WithAllowedCharacters_IsAccepted(string code)
        {
            var command = ValidCommand();
            command.Code = code;

            _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
        }

        // ---------- name ----------

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Name_Empty_Fails(string name)
        {
            var command = ValidCommand();
            command.Name = name;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Name_LongerThan200_Fails()
        {
            var command = ValidCommand();
            command.Name = new string('x', 201);

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Name);
        }

        // ---------- address ----------

        [Fact]
        public void Address_Missing_Fails()
        {
            var command = ValidCommand();
            command.Address = null!;

            _validator.TestValidate(command)
                .ShouldHaveValidationErrorFor(x => x.Address)
                .WithErrorMessage("Address is required.");
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void AddressLine1_Empty_Fails_UnderTheDottedName(string line1)
        {
            var command = ValidCommand();
            command.Address.Line1 = line1;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor("Address.Line1");
        }

        [Fact]
        public void AddressLine1_LongerThan200_Fails()
        {
            var command = ValidCommand();
            command.Address.Line1 = new string('x', 201);

            _validator.TestValidate(command).ShouldHaveValidationErrorFor("Address.Line1");
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void AddressCity_Empty_Fails(string city)
        {
            var command = ValidCommand();
            command.Address.City = city;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor("Address.City");
        }

        [Fact]
        public void AddressCity_LongerThan100_Fails()
        {
            var command = ValidCommand();
            command.Address.City = new string('x', 101);

            _validator.TestValidate(command).ShouldHaveValidationErrorFor("Address.City");
        }

        [Fact]
        public void AddressLine2_LongerThan200_Fails()
        {
            var command = ValidCommand();
            command.Address.Line2 = new string('x', 201);

            _validator.TestValidate(command).ShouldHaveValidationErrorFor("Address.Line2");
        }

        [Fact]
        public void AddressRegion_LongerThan100_Fails()
        {
            var command = ValidCommand();
            command.Address.Region = new string('x', 101);

            _validator.TestValidate(command).ShouldHaveValidationErrorFor("Address.Region");
        }

        [Fact]
        public void AddressPostalCode_LongerThan20_Fails()
        {
            var command = ValidCommand();
            command.Address.PostalCode = new string('1', 21);

            _validator.TestValidate(command).ShouldHaveValidationErrorFor("Address.PostalCode");
        }

        [Theory]
        [InlineData("")]
        [InlineData("I")]
        [InlineData("IND")]
        [InlineData("1N")]
        [InlineData("I-")]
        public void AddressCountryCode_NotTwoLetters_Fails(string countryCode)
        {
            var command = ValidCommand();
            command.Address.CountryCode = countryCode;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor("Address.CountryCode");
        }

        [Fact]
        public void AddressCountryCode_InLowerCase_IsAccepted_BecauseTheDomainNormalizesIt()
        {
            var command = ValidCommand();
            command.Address.CountryCode = "nl";

            _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
        }
    }
}
