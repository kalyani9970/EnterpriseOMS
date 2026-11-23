using EnterpriseOMS.Domain.Common;
using EnterpriseOMS.Domain.Common.Exceptions;
using EnterpriseOMS.Domain.Entities.Identity;
using EnterpriseOMS.Domain.Entities.Tenancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.UnitTests.Entities
{
    public class OrganizationTests
    {
        [Fact]
        public void Create_TrimsNameAndCode_AndStartsActive()
        {
            var organization = Organization.Create("  Acme Traders  ", "  acme-traders  ");

            Assert.Equal("Acme Traders", organization.Name);
            Assert.Equal("acme-traders", organization.Code);
            Assert.True(organization.IsActive);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_EmptyName_Throws(string name)
        {
            var exception = Assert.Throws<DomainException>(() => Organization.Create(name, "acme"));

            Assert.Equal("Organization name is required.", exception.Message);
        }

        [Fact]
        public void Create_NameLongerThan200_Throws()
        {
            var exception = Assert.Throws<DomainException>(() => Organization.Create(new string('x', 201), "acme"));

            Assert.Equal("Organization name must be 200 characters or fewer.", exception.Message);
        }

        [Fact]
        public void Create_NameOfExactly200Characters_Succeeds()
        {
            var organization = Organization.Create(new string('x', 200), "acme");

            Assert.Equal(200, organization.Name.Length);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Acme")]        // uppercase
        [InlineData("ac me")]       // space
        [InlineData("-acme")]       // leading hyphen
        [InlineData("acme-")]       // trailing hyphen
        [InlineData("ac--me")]      // double hyphen
        public void Create_InvalidCode_Throws(string code)
        {
            Assert.Throws<DomainException>(() => Organization.Create("Acme", code));
        }

        [Fact]
        public void Create_CodeLongerThan50_Throws()
        {
            Assert.Throws<DomainException>(() => Organization.Create("Acme", new string('a', 51)));
        }

        [Fact]
        public void Create_CodeOfExactly50Characters_Succeeds()
        {
            var organization = Organization.Create("Acme", new string('a', 50));

            Assert.Equal(50, organization.Code.Length);
        }

        [Theory]
        [InlineData("acme")]
        [InlineData("acme-2")]
        [InlineData("a1-b2-c3")]
        public void Create_ValidCode_Succeeds(string code)
        {
            Assert.Equal(code, Organization.Create("Acme", code).Code);
        }

        [Fact]
        public void Rename_TrimsTheName_AndNeverChangesTheCode()
        {
            var organization = Organization.Create("Acme", "acme");

            organization.Rename("  Acme Holdings  ");

            Assert.Equal("Acme Holdings", organization.Name);
            Assert.Equal("acme", organization.Code);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Rename_EmptyName_ThrowsAndKeepsTheOldName(string name)
        {
            var organization = Organization.Create("Acme", "acme");

            Assert.Throws<DomainException>(() => organization.Rename(name));

            Assert.Equal("Acme", organization.Name);
        }

        [Fact]
        public void Rename_NameLongerThan200_ThrowsAndKeepsTheOldName()
        {
            var organization = Organization.Create("Acme", "acme");

            Assert.Throws<DomainException>(() => organization.Rename(new string('x', 201)));

            Assert.Equal("Acme", organization.Name);
        }

        [Fact]
        public void DeactivateAndActivate_ToggleTheFlag()
        {
            var organization = Organization.Create("Acme", "acme");

            organization.Deactivate();
            Assert.False(organization.IsActive);

            organization.Activate();
            Assert.True(organization.IsActive);
        }

        [Fact]
        public void User_IsATenantEntity()
        {
            Assert.IsAssignableFrom<ITenantEntity>(new User());
        }

        [Fact]
        public void Organization_IsTheTenantRoot_NotATenantEntity()
        {
            Assert.False(typeof(ITenantEntity).IsAssignableFrom(typeof(Organization)));
        }
    }
}
