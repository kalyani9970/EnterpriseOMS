using EnterpriseOMS.Application.Modules.Warehouses.Commands.UpdateWarehouse;
using EnterpriseOMS.Application.Modules.Warehouses.DTOs;
using FluentValidation.TestHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.UnitTests.Validators
{
    public class UpdateWarehouseCommandValidatorTests
    {
        private readonly UpdateWarehouseCommandValidator _validator = new();

        private static UpdateWarehouseCommand ValidCommand() => new()
        {
            Id = 3,
            Version = Guid.NewGuid(),
            Name = "Main Distribution Center",
            Address = new AddressDto { Line1 = "12 Industrial Road", City = "Pune", CountryCode = "IN" }
        };

        [Fact]
        public void ValidCommand_HasNoErrors()
        {
            _validator.TestValidate(ValidCommand()).ShouldNotHaveAnyValidationErrors();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-3)]
        public void Id_NotPositive_Fails(int id)
        {
            var command = ValidCommand();
            command.Id = id;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Id);
        }

        [Fact]
        public void Version_Missing_Fails_BecauseAnEditMustProveWhatItWasBasedOn()
        {
            var command = ValidCommand();
            command.Version = Guid.Empty;

            _validator.TestValidate(command)
                .ShouldHaveValidationErrorFor(x => x.Version)
                .WithErrorMessage("Version is required. Reload the warehouse and try again.");
        }

        [Fact]
        public void Name_Empty_Fails()
        {
            var command = ValidCommand();
            command.Name = "   ";

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Name_LongerThan200_Fails()
        {
            var command = ValidCommand();
            command.Name = new string('x', 201);

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Address_Missing_Fails()
        {
            var command = ValidCommand();
            command.Address = null!;

            _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Address);
        }

        [Fact]
        public void AddressCity_Empty_Fails_UnderTheDottedName()
        {
            var command = ValidCommand();
            command.Address.City = "";

            _validator.TestValidate(command).ShouldHaveValidationErrorFor("Address.City");
        }

        [Fact]
        public void AddressCountryCode_Invalid_Fails()
        {
            var command = ValidCommand();
            command.Address.CountryCode = "IND";

            _validator.TestValidate(command).ShouldHaveValidationErrorFor("Address.CountryCode");
        }
    }
}
