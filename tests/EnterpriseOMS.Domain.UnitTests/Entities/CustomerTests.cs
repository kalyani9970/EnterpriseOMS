using EnterpriseOMS.Domain.Common;
using EnterpriseOMS.Domain.Common.Exceptions;
using EnterpriseOMS.Domain.Entities.Sales;
using EnterpriseOMS.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.UnitTests.Entities
{
    public class CustomerTests
    {
        private static Address MakeAddress() => Address.Create("5 Market Street", null, "Rotterdam", null, "3011 AA", "NL");

        private static Customer Make(string code = "ACME-01", string name = "Acme Retail", string? email = "buyer@acme.test", string? phone = "+31 10 123 4567") =>
            Customer.Create(7, code, name, email, phone, MakeAddress());

        [Fact]
        public void Create_WithValidDetails_StartsActive_WithTheGivenValues()
        {
            var customer = Make();

            Assert.Equal(7, customer.OrganizationId);
            Assert.Equal("ACME-01", customer.Code);
            Assert.Equal("Acme Retail", customer.Name);
            Assert.Equal("buyer@acme.test", customer.Email);
            Assert.Equal("+31 10 123 4567", customer.Phone);
            Assert.Equal("Rotterdam", customer.BillingAddress.City);
            Assert.True(customer.IsActive);
            Assert.NotEqual(Guid.Empty, customer.Version);
        }

        [Fact]
        public void Create_TrimsTheCode_AndKeepsAnUpperCaseCopyForUniqueness()
        {
            var customer = Make(code: "  acme-01  ");

            Assert.Equal("acme-01", customer.Code);
            Assert.Equal("ACME-01", customer.NormalizedCode);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_WithoutACode_Throws(string code)
        {
            var exception = Assert.Throws<DomainException>(() => Make(code: code));

            Assert.Equal("Customer code is required.", exception.Message);
        }

        [Theory]
        [InlineData("-x")]
        [InlineData("a b")]
        [InlineData("a#b")]
        [InlineData("é1")]
        public void Create_WithAnInvalidCode_Throws(string code)
        {
            var exception = Assert.Throws<DomainException>(() => Make(code: code));

            Assert.Contains("may contain only letters, digits", exception.Message);
        }

        [Fact]
        public void Create_ACodeOfExactly30Characters_IsAccepted_And31IsNot()
        {
            Assert.Equal(30, Make(code: new string('A', 30)).Code.Length);

            var exception = Assert.Throws<DomainException>(() => Make(code: new string('A', 31)));
            Assert.Contains("30 characters", exception.Message);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-3)]
        public void Create_WithoutAnOrganization_Throws(int organizationId)
        {
            var exception = Assert.Throws<DomainException>(
                () => Customer.Create(organizationId, "C-1", "Name", null, null, MakeAddress()));

            Assert.Equal("A customer must belong to an organization.", exception.Message);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_WithoutAName_Throws(string name)
        {
            var exception = Assert.Throws<DomainException>(() => Make(name: name));

            Assert.Equal("Customer name is required.", exception.Message);
        }

        [Fact]
        public void Create_ANameOfExactly200Characters_IsAccepted_And201IsNot()
        {
            Assert.Equal(200, Make(name: new string('n', 200)).Name.Length);

            Assert.Throws<DomainException>(() => Make(name: new string('n', 201)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_ABlankEmail_BecomesNothing(string? email)
        {
            Assert.Null(Make(email: email).Email);
        }

        [Fact]
        public void Create_TrimsTheEmail()
        {
            Assert.Equal("buyer@acme.test", Make(email: "  buyer@acme.test  ").Email);
        }

        [Theory]
        [InlineData("plain")]
        [InlineData("a@b")]
        [InlineData("a b@c.d")]
        [InlineData("@c.d")]
        [InlineData("a@@c.d")]
        public void Create_AnInvalidEmail_Throws(string email)
        {
            var exception = Assert.Throws<DomainException>(() => Make(email: email));

            Assert.Equal("Email is not a valid email address.", exception.Message);
        }

        [Fact]
        public void Create_AnEmailLongerThan200Characters_Throws()
        {
            var tooLong = new string('a', 195) + "@x.org";

            Assert.Throws<DomainException>(() => Make(email: tooLong));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("  ")]
        public void Create_ABlankPhone_BecomesNothing(string? phone)
        {
            Assert.Null(Make(phone: phone).Phone);
        }

        [Theory]
        [InlineData("+31 10 123 4567")]
        [InlineData("(020) 555-0100")]
        [InlineData("12345")]
        public void Create_AValidPhone_IsAccepted(string phone)
        {
            Assert.Equal(phone, Make(phone: phone).Phone);
        }

        [Theory]
        [InlineData("abcdef")]
        [InlineData("1234")]
        [InlineData("12345678901234567890123456789012")]
        public void Create_AnInvalidPhone_Throws(string phone)
        {
            Assert.Throws<DomainException>(() => Make(phone: phone));
        }

        [Fact]
        public void Create_WithoutABillingAddress_Throws()
        {
            var exception = Assert.Throws<DomainException>(() => Customer.Create(7, "C-1", "Name", null, null, null!));

            Assert.Equal("A customer needs a billing address.", exception.Message);
        }

        [Fact]
        public void Update_ChangesTheDetails_ButNeverTheCode_AndGivesANewVersion()
        {
            var customer = Make();
            var version = customer.Version;

            customer.Update("Acme Wholesale", null, "+31 20 555 0000", Address.Create("1 Dock Rd", null, "Antwerp", null, null, "BE"));

            Assert.Equal("ACME-01", customer.Code);
            Assert.Equal("Acme Wholesale", customer.Name);
            Assert.Null(customer.Email);
            Assert.Equal("+31 20 555 0000", customer.Phone);
            Assert.Equal("Antwerp", customer.BillingAddress.City);
            Assert.NotEqual(version, customer.Version);
        }

        [Fact]
        public void Update_WithAnInvalidDetail_Throws_AndChangesNothing()
        {
            var customer = Make();
            var version = customer.Version;

            Assert.Throws<DomainException>(() => customer.Update("New Name", "not-an-email", null, MakeAddress()));

            Assert.Equal("Acme Retail", customer.Name);
            Assert.Equal("buyer@acme.test", customer.Email);
            Assert.Equal(version, customer.Version);
        }

        [Fact]
        public void Deactivate_ThenActivate_TogglesTheFlag_AndGivesNewVersions()
        {
            var customer = Make();
            var v0 = customer.Version;

            customer.Deactivate();
            var v1 = customer.Version;
            customer.Activate();

            Assert.True(customer.IsActive);
            Assert.NotEqual(v0, v1);
            Assert.NotEqual(v1, customer.Version);
        }

        [Fact]
        public void DeactivatingAnInactiveCustomer_ChangesNothing()
        {
            var customer = Make();
            customer.Deactivate();
            var version = customer.Version;

            customer.Deactivate();

            Assert.False(customer.IsActive);
            Assert.Equal(version, customer.Version);
        }

        [Fact]
        public void ActivatingAnActiveCustomer_ChangesNothing()
        {
            var customer = Make();
            var version = customer.Version;

            customer.Activate();

            Assert.Equal(version, customer.Version);
        }

        [Fact]
        public void Customer_IsATenantEntity()
        {
            Assert.IsAssignableFrom<ITenantEntity>(Make());
        }
    }
}
