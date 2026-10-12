using EnterpriseOMS.Domain.Entities.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.UnitTests.Entities
{
    public class UserTests
    {
        [Fact]
        public void NewUser_IsActiveByDefault()
        {
            var user = new User();

            Assert.True(user.IsActive);
            Assert.Null(user.LastLoginAt);
        }

        [Fact]
        public void Deactivate_MarksUserInactive()
        {
            var user = new User();

            user.Deactivate();

            Assert.False(user.IsActive);
        }

        [Fact]
        public void RecordLogin_StoresTimestamp()
        {
            var user = new User();
            var loginTime = new DateTime(2026, 3, 1, 9, 30, 0, DateTimeKind.Utc);

            user.RecordLogin(loginTime);

            Assert.Equal(loginTime, user.LastLoginAt);
        }
    }
}
