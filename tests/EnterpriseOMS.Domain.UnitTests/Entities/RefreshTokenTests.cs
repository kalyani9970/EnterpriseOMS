using EnterpriseOMS.Domain.Entities.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.UnitTests.Entities
{
    public class RefreshTokenTests
    {
        [Fact]
        public void IsActive_NotRevokedAndNotExpired_ReturnsTrue()
        {
            var token = new RefreshToken
            {
                Token = "abc",
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            };

            Assert.True(token.IsActive);
        }

        [Fact]
        public void IsActive_Revoked_ReturnsFalse()
        {
            var token = new RefreshToken
            {
                Token = "abc",
                ExpiresAt = DateTime.UtcNow.AddDays(1),
                RevokedAt = DateTime.UtcNow
            };

            Assert.False(token.IsActive);
        }

        [Fact]
        public void IsActive_Expired_ReturnsFalse()
        {
            var token = new RefreshToken
            {
                Token = "abc",
                ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
            };

            Assert.False(token.IsActive);
        }
    }
}
