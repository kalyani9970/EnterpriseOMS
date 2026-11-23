using EnterpriseOMS.Application.Common.Exceptions;
using EnterpriseOMS.Application.IntegrationTests.Support;
using EnterpriseOMS.Application.Modules.Auth.Commands.Login;
using EnterpriseOMS.Application.Modules.Auth.Commands.RefreshTokens;
using EnterpriseOMS.Application.Modules.Auth.DTOs;
using EnterpriseOMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.IntegrationTests.Handlers
{
    /// <summary>A deactivated organization (or user) must not be able to sign in or keep a session alive.</summary>
    public class OrganizationLifecycleTests : IntegrationTestBase
    {
        private async Task SetOrganizationActiveAsync(string code, bool active)
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var organization = await db.Organizations.IgnoreQueryFilters().SingleAsync(o => o.Code == code);

            if (active) organization.Activate();
            else organization.Deactivate();

            await db.SaveChangesAsync();
        }

        private async Task<AuthResultDto> LoginAsync(string email, string password = "Passw0rd1")
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<LoginCommandHandler>();

            return await handler.Handle(new LoginCommand { Email = email, Password = password }, CancellationToken.None);
        }

        private async Task<AuthResultDto> RefreshAsync(string refreshToken)
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<RefreshTokenCommandHandler>();

            return await handler.Handle(new RefreshTokenCommand { RefreshToken = refreshToken }, CancellationToken.None);
        }

        [Fact]
        public async Task Login_DeactivatedOrganization_ThrowsForbidden()
        {
            await RegisterAsync("alice@acme.test", "Acme Traders");
            await SetOrganizationActiveAsync("acme-traders", active: false);

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => LoginAsync("alice@acme.test"));
        }

        [Fact]
        public async Task Login_DeactivatedOrganization_WithWrongPassword_DoesNotRevealTheStatus()
        {
            await RegisterAsync("alice@acme.test", "Acme Traders");
            await SetOrganizationActiveAsync("acme-traders", active: false);

            // A stranger guessing passwords must see the same generic error as for any other account.
            await Assert.ThrowsAsync<ValidationException>(() => LoginAsync("alice@acme.test", "Wrong-Password1"));
        }

        [Fact]
        public async Task Login_AfterTheOrganizationIsReactivated_Succeeds()
        {
            await RegisterAsync("alice@acme.test", "Acme Traders");
            await SetOrganizationActiveAsync("acme-traders", active: false);
            await SetOrganizationActiveAsync("acme-traders", active: true);

            var result = await LoginAsync("alice@acme.test");

            Assert.Equal("Admin", result.Role);
        }

        [Fact]
        public async Task Login_OtherOrganizationsAreUnaffected()
        {
            await RegisterAsync("alice@acme.test", "Acme Traders");
            await RegisterAsync("bob@globex.test", "Globex");
            await SetOrganizationActiveAsync("acme-traders", active: false);

            var result = await LoginAsync("bob@globex.test");

            Assert.Equal("bob@globex.test", result.Email);
        }

        [Fact]
        public async Task Refresh_DeactivatedOrganization_ThrowsForbidden()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            await SetOrganizationActiveAsync("acme-traders", active: false);

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => RefreshAsync(alice.RefreshToken));
        }

        [Fact]
        public async Task Refresh_DeactivatedUser_ThrowsForbidden()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");

            using (var scope = App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var user = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == "alice@acme.test");
                user.Deactivate();
                await db.SaveChangesAsync();
            }

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => RefreshAsync(alice.RefreshToken));
        }
    }
}
