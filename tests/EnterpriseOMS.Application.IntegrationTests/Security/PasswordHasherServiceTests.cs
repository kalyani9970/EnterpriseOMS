using EnterpriseOMS.Infrastructure.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.IntegrationTests.Security
{
    public class PasswordHasherServiceTests
    {
        private readonly PasswordHasherService _hasher = new();

        [Fact]
        public void Hash_DoesNotReturnThePlainPassword()
        {
            var hash = _hasher.Hash("Passw0rd1");

            Assert.NotEqual("Passw0rd1", hash);
            Assert.False(string.IsNullOrWhiteSpace(hash));
        }

        [Fact]
        public void Hash_SamePasswordTwice_ProducesDifferentHashesBecauseOfSalt()
        {
            var first = _hasher.Hash("Passw0rd1");
            var second = _hasher.Hash("Passw0rd1");

            Assert.NotEqual(first, second);
        }

        [Fact]
        public void Verify_CorrectPassword_ReturnsTrue()
        {
            var hash = _hasher.Hash("Passw0rd1");

            Assert.True(_hasher.Verify(hash, "Passw0rd1"));
        }

        [Fact]
        public void Verify_WrongPassword_ReturnsFalse()
        {
            var hash = _hasher.Hash("Passw0rd1");

            Assert.False(_hasher.Verify(hash, "passw0rd1"));
        }
    }
}
