using EnterpriseOMS.Application.Common.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.UnitTests.Exceptions
{
    public class ConcurrencyExceptionTests
    {
        [Fact]
        public void Message_NamesTheEntityAndTellsTheCallerToReload()
        {
            var exception = new ConcurrencyException("Product", 5);

            Assert.Equal("Product (5) was changed by someone else. Reload it and try again.", exception.Message);
        }
    }
}
