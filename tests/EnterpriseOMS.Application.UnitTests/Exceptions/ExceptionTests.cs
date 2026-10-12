using EnterpriseOMS.Application.Common.Exceptions;
using FluentValidation.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.UnitTests.Exceptions
{
    public class ExceptionTests
    {
        [Fact]
        public void ValidationException_GroupsFailuresByProperty()
        {
            var failures = new[]
            {
            new ValidationFailure("Email", "Email is required."),
            new ValidationFailure("Email", "A valid email address is required."),
            new ValidationFailure("Password", "Password is required.")
        };

            var exception = new ValidationException(failures);

            Assert.Equal(2, exception.Errors.Count);
            Assert.Equal(2, exception.Errors["Email"].Length);
            Assert.Single(exception.Errors["Password"]);
        }

        [Fact]
        public void ValidationException_Default_HasNoErrors()
        {
            var exception = new ValidationException();

            Assert.Empty(exception.Errors);
        }

        [Fact]
        public void NotFoundException_FormatsEntityNameAndKey()
        {
            var exception = new NotFoundException("User", 5);

            Assert.Equal("Entity \"User\" (5) was not found.", exception.Message);
        }

        [Fact]
        public void ForbiddenAccessException_HasDefaultMessage()
        {
            var exception = new ForbiddenAccessException();

            Assert.Equal("You do not have permission to perform this action.", exception.Message);
        }
    }
}
