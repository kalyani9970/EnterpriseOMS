using EnterpriseOMS.Application.Common.Exceptions;
using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Application.IntegrationTests.Support;
using EnterpriseOMS.Application.Modules.Auth.Commands.Login;
using EnterpriseOMS.Application.Modules.Auth.Commands.Logout;
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
    public class LogoutCommandHandlerTests : IntegrationTestBase
    {
        private async Task<AuthResultDto> LoginAsAdminAsync()
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<LoginCommandHandler>();

            return await handler.Handle(
                new LoginCommand { Email = TestHost.AdminEmail, Password = TestHost.AdminPassword },
                CancellationToken.None);
        }

        private async Task LogoutAsync(string refreshToken)
        {
            using var scope = App.Services.CreateScope();

            // Built by hand: the host registers only the other Auth handlers, and this one needs just two services.
            var handler = new LogoutCommandHandler(
                scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
                scope.ServiceProvider.GetRequiredService<IDateTime>());

            var result = await handler.Handle(
                new LogoutCommand { RefreshToken = refreshToken },
                CancellationToken.None);

            Assert.True(result.Succeeded);
        }

        private async Task<DateTime?> RevokedAtAsync(string refreshToken)
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var token = await db.RefreshTokens.SingleAsync(t => t.Token == refreshToken);
            return token.RevokedAt;
        }

        [Fact]
        public async Task Logout_RevokesTheRefreshToken()
        {
            var login = await LoginAsAdminAsync();

            await LogoutAsync(login.RefreshToken);

            Assert.Equal(App.Clock.UtcNow, await RevokedAtAsync(login.RefreshToken));
        }

        [Fact]
        public async Task Logout_ThenRefreshWithSameToken_IsRejected()
        {
            var login = await LoginAsAdminAsync();
            await LogoutAsync(login.RefreshToken);

            using var scope = App.Services.CreateScope();
            var refreshHandler = scope.ServiceProvider.GetRequiredService<RefreshTokenCommandHandler>();

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => refreshHandler.Handle(
                new RefreshTokenCommand { RefreshToken = login.RefreshToken },
                CancellationToken.None));
        }

        [Fact]
        public async Task Logout_UnknownToken_SucceedsSilently()
        {
            await LogoutAsync("this-token-does-not-exist");
        }

        [Fact]
        public async Task Logout_CalledTwice_KeepsTheOriginalRevocationTime()
        {
            var login = await LoginAsAdminAsync();
            await LogoutAsync(login.RefreshToken);
            var firstRevocation = await RevokedAtAsync(login.RefreshToken);

            App.Clock.Advance(TimeSpan.FromMinutes(10));
            await LogoutAsync(login.RefreshToken);

            Assert.Equal(firstRevocation, await RevokedAtAsync(login.RefreshToken));
        }

        [Fact]
        public async Task Logout_OnlyEndsThatSession_OtherSessionsStayActive()
        {
            var laptop = await LoginAsAdminAsync();
            var phone = await LoginAsAdminAsync();

            await LogoutAsync(laptop.RefreshToken);

            Assert.NotNull(await RevokedAtAsync(laptop.RefreshToken));
            Assert.Null(await RevokedAtAsync(phone.RefreshToken));
        }
    }
}
