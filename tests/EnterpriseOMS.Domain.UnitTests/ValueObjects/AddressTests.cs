using EnterpriseOMS.Domain.Common.Exceptions;
using EnterpriseOMS.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.UnitTests.ValueObjects
{
    public class AddressTests
    {
        private static Address Make(
        string line1 = "12 Industrial Road",
        string? line2 = null,
        string city = "Pune",
        string? region = "MH",
        string? postalCode = "411001",
        string countryCode = "IN") =>
        Address.Create(line1, line2, city, region, postalCode, countryCode);

        [Fact]
        public void Create_StoresTheDetails_Trimmed()
        {
            var address = Make(line1: "  12 Industrial Road ", line2: "  Gate 4 ", city: " Pune ", region: " MH ", postalCode: " 411001 ");

            Assert.Equal("12 Industrial Road", address.Line1);
            Assert.Equal("Gate 4", address.Line2);
            Assert.Equal("Pune", address.City);
            Assert.Equal("MH", address.Region);
            Assert.Equal("411001", address.PostalCode);
            Assert.Equal("IN", address.CountryCode);
        }

        [Theory]
        [InlineData("in", "IN")]
        [InlineData(" nl ", "NL")]
        [InlineData("De", "DE")]
        public void Create_CountryCode_IsTrimmedAndUpperCased(string typed, string stored)
        {
            Assert.Equal(stored, Make(countryCode: typed).CountryCode);
        }

        [Fact]
        public void Create_BlankOptionalFields_BecomeNull()
        {
            var address = Make(line2: "   ", region: "", postalCode: null);

            Assert.Null(address.Line2);
            Assert.Null(address.Region);
            Assert.Null(address.PostalCode);
        }

        [Fact]
        public void TwoAddressesWithTheSameValues_AreEqual_AsAValueObjectShouldBe()
        {
            Assert.Equal(Make(), Make());
            Assert.NotEqual(Make(), Make(city: "Mumbai"));
            Assert.True(Make() == Make());
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_EmptyLine1_Throws(string line1)
        {
            var exception = Assert.Throws<DomainException>(() => Make(line1: line1));

            Assert.Equal("Address line 1 is required.", exception.Message);
        }

        [Fact]
        public void Create_Line1LongerThan200_Throws()
        {
            Assert.Throws<DomainException>(() => Make(line1: new string('x', 201)));
        }

        [Fact]
        public void Create_FieldsAtTheirLimits_AreAccepted()
        {
            var address = Make(
                line1: new string('x', 200),
                line2: new string('x', 200),
                city: new string('x', 100),
                region: new string('x', 100),
                postalCode: new string('1', 20));

            Assert.Equal(200, address.Line1.Length);
            Assert.Equal(20, address.PostalCode!.Length);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_EmptyCity_Throws(string city)
        {
            var exception = Assert.Throws<DomainException>(() => Make(city: city));

            Assert.Equal("City is required.", exception.Message);
        }

        [Fact]
        public void Create_CityLongerThan100_Throws()
        {
            Assert.Throws<DomainException>(() => Make(city: new string('x', 101)));
        }

        [Fact]
        public void Create_Line2LongerThan200_Throws()
        {
            Assert.Throws<DomainException>(() => Make(line2: new string('x', 201)));
        }

        [Fact]
        public void Create_RegionLongerThan100_Throws()
        {
            Assert.Throws<DomainException>(() => Make(region: new string('x', 101)));
        }

        [Fact]
        public void Create_PostalCodeLongerThan20_Throws()
        {
            Assert.Throws<DomainException>(() => Make(postalCode: new string('1', 21)));
        }

        [Theory]
        [InlineData("")]
        [InlineData("I")]
        [InlineData("IND")]
        [InlineData("1N")]
        [InlineData("I-")]
        public void Create_CountryCodeThatIsNotTwoLetters_Throws(string countryCode)
        {
            var exception = Assert.Throws<DomainException>(() => Make(countryCode: countryCode));

            Assert.Contains("2-letter", exception.Message);
        }
    }
}
