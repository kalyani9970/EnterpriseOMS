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
    public class RefreshTokenCommandHandlerTests : IntegrationTestBase
    {
        private async Task<AuthResultDto> LoginAsAdminAsync()
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<LoginCommandHandler>();

            return await handler.Handle(
                new LoginCommand { Email = TestHost.AdminEmail, Password = TestHost.AdminPassword },
                CancellationToken.None);
        }

        private async Task<AuthResultDto> RefreshAsync(string refreshToken)
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<RefreshTokenCommandHandler>();

            return await handler.Handle(
                new RefreshTokenCommand { RefreshToken = refreshToken },
                CancellationToken.None);
        }

        [Fact]
        public async Task Refresh_ValidToken_RotatesTokenAndIssuesNewAccessToken()
        {
            var login = await LoginAsAdminAsync();

            var refreshed = await RefreshAsync(login.RefreshToken);

            Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
            Assert.False(string.IsNullOrWhiteSpace(refreshed.AccessToken));
            Assert.Equal("Admin", refreshed.Role);

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var oldToken = await db.RefreshTokens.SingleAsync(t => t.Token == login.RefreshToken);
            var newToken = await db.RefreshTokens.SingleAsync(t => t.Token == refreshed.RefreshToken);

            Assert.NotNull(oldToken.RevokedAt);
            Assert.Equal(refreshed.RefreshToken, oldToken.ReplacedByToken);
            Assert.True(newToken.IsActive);
        }

        [Fact]
        public async Task Refresh_ReusedToken_ThrowsForbiddenAndRevokesAllActiveTokens()
        {
            var login = await LoginAsAdminAsync();
            await RefreshAsync(login.RefreshToken);   // legitimate use, the token is now revoked

            // A second use of the same token looks like theft.
            await Assert.ThrowsAsync<ForbiddenAccessException>(() => RefreshAsync(login.RefreshToken));

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stillActive = await db.RefreshTokens.CountAsync(t => t.RevokedAt == null);

            Assert.Equal(0, stillActive);
        }

        [Fact]
        public async Task Refresh_UnknownToken_ThrowsNotFound()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => RefreshAsync("does-not-exist"));
        }

        [Fact]
        public async Task Refresh_ExpiredToken_ThrowsForbidden()
        {
            var login = await LoginAsAdminAsync();

            using (var scope = App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var token = await db.RefreshTokens.SingleAsync(t => t.Token == login.RefreshToken);
                token.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
                await db.SaveChangesAsync();
            }

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => RefreshAsync(login.RefreshToken));
        }
    }
}
