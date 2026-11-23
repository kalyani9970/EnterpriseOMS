using EnterpriseOMS.Application.Modules.Organizations;
using EnterpriseOMS.Domain.Entities.Tenancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.UnitTests.Organizations
{
    public class OrganizationCodeTests
    {
        [Theory]
        [InlineData("Acme Traders", "acme-traders")]
        [InlineData("  Acme   Traders, Inc.  ", "acme-traders-inc")]
        [InlineData("ACME-CORP", "acme-corp")]
        [InlineData("Café Münch", "caf-m-nch")]             // non-ASCII letters are dropped
        [InlineData("株式会社", "org")]                        // nothing usable left: fall back to "org"
        [InlineData("***", "org")]
        [InlineData("A", "a")]
        [InlineData("Tom & Jerry's #1", "tom-jerry-s-1")]
        [InlineData("---x---", "x")]
        public void FromName_BuildsAUrlSafeSlug(string name, string expected)
        {
            Assert.Equal(expected, OrganizationCode.FromName(name));
        }

        [Fact]
        public void FromName_TruncatesLongNamesTo40Characters()
        {
            var code = OrganizationCode.FromName(new string('a', 60));

            Assert.Equal(OrganizationCode.BaseMaxLength, code.Length);
        }

        [Fact]
        public void FromName_DoesNotLeaveATrailingHyphenWhenTruncating()
        {
            // 39 letters, then a space that becomes a hyphen exactly at the cut-off point.
            var code = OrganizationCode.FromName(new string('a', 39) + " b");

            Assert.Equal(new string('a', 39), code);
        }

        [Fact]
        public void FromName_LeavesRoomForANumericSuffixWithinTheCodeLimit()
        {
            var code = OrganizationCode.FromName(new string('a', 200));

            Assert.True(code.Length + "-999999999".Length <= Organization.CodeMaxLength);
        }

        [Theory]
        [InlineData("Acme Traders")]
        [InlineData("株式会社")]
        [InlineData("***")]
        [InlineData("  ---  ")]
        [InlineData("Café Münch")]
        [InlineData("A very long organization name that keeps going and going well past the limit")]
        public void FromName_AlwaysProducesACodeThatTheDomainAccepts(string name)
        {
            var organization = Organization.Create(name, OrganizationCode.FromName(name));

            Assert.True(organization.Code.Length > 0);
        }
    }
}
