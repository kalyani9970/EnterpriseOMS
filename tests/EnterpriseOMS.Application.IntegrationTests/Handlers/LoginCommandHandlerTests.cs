using EnterpriseOMS.Application.Common.Exceptions;
using EnterpriseOMS.Application.IntegrationTests.Support;
using EnterpriseOMS.Application.Modules.Auth.Commands.Login;
using EnterpriseOMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.IntegrationTests.Handlers
{
    public class LoginCommandHandlerTests : IntegrationTestBase
    {
        private async Task<T> RunAsync<T>(Func<LoginCommandHandler, Task<T>> action)
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<LoginCommandHandler>();
            return await action(handler);
        }

        [Fact]
        public async Task Login_ValidCredentials_ReturnsAdminTokensWithAllPermissions()
        {
            var result = await RunAsync(h => h.Handle(
                new LoginCommand { Email = TestHost.AdminEmail, Password = TestHost.AdminPassword },
                CancellationToken.None));

            Assert.Equal("Admin", result.Role);
            Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));

            int permissionCount;
            using (var scope = App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                permissionCount = await db.Permissions.CountAsync();
            }

            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);
            Assert.Equal(permissionCount, jwt.Claims.Count(c => c.Type == "permission"));
            Assert.Contains(jwt.Claims, c => c.Type == "permission" && c.Value == "roles.view");
        }

        [Fact]
        public async Task Login_EmailIsCaseInsensitive()
        {
            var result = await RunAsync(h => h.Handle(
                new LoginCommand { Email = TestHost.AdminEmail.ToUpperInvariant(), Password = TestHost.AdminPassword },
                CancellationToken.None));

            Assert.Equal("Admin", result.Role);
        }

        [Fact]
        public async Task Login_RecordsLastLoginTime()
        {
            await RunAsync(h => h.Handle(
                new LoginCommand { Email = TestHost.AdminEmail, Password = TestHost.AdminPassword },
                CancellationToken.None));

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == TestHost.AdminEmail);

            Assert.Equal(App.Clock.UtcNow, user.LastLoginAt);
        }

        [Fact]
        public async Task Login_WrongPassword_ThrowsGenericValidationError()
        {
            var exception = await Assert.ThrowsAsync<ValidationException>(() => RunAsync(h => h.Handle(
                new LoginCommand { Email = TestHost.AdminEmail, Password = "Wrong-Password1" },
                CancellationToken.None)));

            Assert.Equal("Invalid email or password.", Assert.Single(exception.Errors["Email"]));
        }

        [Fact]
        public async Task Login_UnknownEmail_ReturnsSameErrorAsWrongPassword()
        {
            // Identical errors for both cases prevent user enumeration.
            var unknownUser = await Assert.ThrowsAsync<ValidationException>(() => RunAsync(h => h.Handle(
                new LoginCommand { Email = "nobody@example.com", Password = "Passw0rd1" },
                CancellationToken.None)));

            var wrongPassword = await Assert.ThrowsAsync<ValidationException>(() => RunAsync(h => h.Handle(
                new LoginCommand { Email = TestHost.AdminEmail, Password = "Wrong-Password1" },
                CancellationToken.None)));

            Assert.Equal(wrongPassword.Errors["Email"], unknownUser.Errors["Email"]);
        }

        [Fact]
        public async Task Login_InactiveUser_ThrowsForbidden()
        {
            using (var scope = App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var user = await db.Users.SingleAsync(u => u.Email == TestHost.AdminEmail);
                user.Deactivate();
                await db.SaveChangesAsync();
            }

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => RunAsync(h => h.Handle(
                new LoginCommand { Email = TestHost.AdminEmail, Password = TestHost.AdminPassword },
                CancellationToken.None)));
        }
    }
}
