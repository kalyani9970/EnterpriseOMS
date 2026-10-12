using EnterpriseOMS.Application.Common.Exceptions;
using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Application.IntegrationTests.Support;
using EnterpriseOMS.Application.Modules.Auth.Commands.Register;
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
    public class RegisterCommandHandlerTests : IntegrationTestBase
    {
        private static RegisterCommand NewCommand(string email = "jane@example.com") => new()
        {
            FullName = "Jane Doe",
            Email = email,
            Password = "Passw0rd1",
            OrganizationId = 1
        };

        [Fact]
        public async Task Register_ValidData_ReturnsEmployeeWithTokens()
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<RegisterCommandHandler>();

            var result = await handler.Handle(NewCommand(), CancellationToken.None);

            Assert.Equal("Employee", result.Role);
            Assert.Equal("Jane Doe", result.FullName);
            Assert.Equal("jane@example.com", result.Email);
            Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
            Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
            Assert.Equal(App.Clock.UtcNow.AddMinutes(15), result.AccessTokenExpiresAt);
        }

        [Fact]
        public async Task Register_PersistsHashedPasswordAndOneRefreshToken()
        {
            using (var scope = App.Services.CreateScope())
            {
                var handler = scope.ServiceProvider.GetRequiredService<RegisterCommandHandler>();
                await handler.Handle(NewCommand(), CancellationToken.None);
            }

            using var verifyScope = App.Services.CreateScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hasher = verifyScope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            var user = await db.Users
                .Include(u => u.RefreshTokens)
                .SingleAsync(u => u.NormalizedEmail == "JANE@EXAMPLE.COM");

            Assert.NotEqual("Passw0rd1", user.PasswordHash);
            Assert.True(hasher.Verify(user.PasswordHash, "Passw0rd1"));
            Assert.Single(user.RefreshTokens);
            Assert.True(user.IsActive);
        }

        [Fact]
        public async Task Register_TrimsEmailAndName()
        {
            var command = NewCommand("  jane@example.com  ");
            command.FullName = "  Jane Doe  ";

            using (var scope = App.Services.CreateScope())
            {
                var handler = scope.ServiceProvider.GetRequiredService<RegisterCommandHandler>();
                await handler.Handle(command, CancellationToken.None);
            }

            using var verifyScope = App.Services.CreateScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(u => u.NormalizedEmail == "JANE@EXAMPLE.COM");

            Assert.Equal("jane@example.com", user.Email);
            Assert.Equal("Jane Doe", user.FullName);
        }

        [Fact]
        public async Task Register_DuplicateEmail_IgnoringCase_ThrowsValidationException()
        {
            using (var scope = App.Services.CreateScope())
            {
                var handler = scope.ServiceProvider.GetRequiredService<RegisterCommandHandler>();
                await handler.Handle(NewCommand("jane@example.com"), CancellationToken.None);
            }

            using var secondScope = App.Services.CreateScope();
            var secondHandler = secondScope.ServiceProvider.GetRequiredService<RegisterCommandHandler>();

            var exception = await Assert.ThrowsAsync<ValidationException>(
                () => secondHandler.Handle(NewCommand("JANE@Example.COM"), CancellationToken.None));

            Assert.True(exception.Errors.ContainsKey("Email"));
        }

        [Fact]
        public async Task Register_WhenEmployeeRoleMissing_ThrowsNotFound()
        {
            using (var scope = App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var employee = await db.Roles.SingleAsync(r => r.Name == "Employee");
                db.Roles.Remove(employee);
                await db.SaveChangesAsync();
            }

            using var handlerScope = App.Services.CreateScope();
            var handler = handlerScope.ServiceProvider.GetRequiredService<RegisterCommandHandler>();

            await Assert.ThrowsAsync<NotFoundException>(
                () => handler.Handle(NewCommand(), CancellationToken.None));
        }
    }
}
