using System.IdentityModel.Tokens.Jwt;
using EnterpriseOMS.Application.Common.Exceptions;
using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Application.IntegrationTests.Support;
using EnterpriseOMS.Application.Modules.Auth.Commands.Register;
using EnterpriseOMS.Application.Modules.Auth.DTOs;
using EnterpriseOMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseOMS.Application.IntegrationTests.Handlers;

public class RegisterCommandHandlerTests : IntegrationTestBase
{
    private static RegisterCommand NewCommand(
        string email = "jane@example.com",
        string organizationName = "Acme Traders") => new()
        {
            FullName = "Jane Doe",
            Email = email,
            Password = "Passw0rd1",
            OrganizationName = organizationName
        };

    private async Task<AuthResultDto> RunAsync(RegisterCommand command)
    {
        using var scope = App.Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<RegisterCommandHandler>();

        return await handler.Handle(command, CancellationToken.None);
    }

    [Fact]
    public async Task Register_ValidData_ReturnsAdminWithTokens()
    {
        var result = await RunAsync(NewCommand());

        Assert.Equal("Admin", result.Role);
        Assert.Equal("Jane Doe", result.FullName);
        Assert.Equal("jane@example.com", result.Email);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
        Assert.Equal(App.Clock.UtcNow.AddMinutes(15), result.AccessTokenExpiresAt);
    }

    [Fact]
    public async Task Register_CreatesTheOrganizationAndItsAdminTogether()
    {
        await RunAsync(NewCommand(organizationName: "  Acme Traders  "));

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = await db.Users
            .Include(u => u.Organization)
            .Include(u => u.Role)
            .SingleAsync(u => u.NormalizedEmail == "JANE@EXAMPLE.COM");

        Assert.Equal("Acme Traders", user.Organization.Name);
        Assert.Equal("acme-traders", user.Organization.Code);
        Assert.True(user.Organization.IsActive);
        Assert.Equal(user.Organization.Id, user.OrganizationId);
        Assert.Equal("Admin", user.Role.Name);
    }

    [Fact]
    public async Task Register_TokenCarriesTheOrganizationAndEveryPermission()
    {
        var result = await RunAsync(NewCommand());
        var organizationId = await OrganizationIdOfAsync(result.UserId);

        int permissionCount;
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            permissionCount = await db.Permissions.CountAsync();
        }

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);

        Assert.Equal(organizationId.ToString(), jwt.Claims.Single(c => c.Type == "org_id").Value);
        Assert.Equal(permissionCount, jwt.Claims.Count(c => c.Type == "permission"));
        Assert.Contains(jwt.Claims, c => c.Type == "permission" && c.Value == "organizations.update");
    }

    [Fact]
    public async Task Register_SameOrganizationNameTwice_GetsNumberedCodes()
    {
        await RunAsync(NewCommand("first@example.com", "Acme Traders"));
        await RunAsync(NewCommand("second@example.com", "Acme Traders"));
        await RunAsync(NewCommand("third@example.com", "Acme Traders"));

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var codes = await db.Organizations
            .Where(o => o.Name == "Acme Traders")
            .OrderBy(o => o.Code)
            .Select(o => o.Code)
            .ToListAsync();

        Assert.Equal(new[] { "acme-traders", "acme-traders-2", "acme-traders-3" }, codes);
    }

    [Fact]
    public async Task Register_PersistsHashedPasswordAndOneRefreshToken()
    {
        await RunAsync(NewCommand());

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

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

        await RunAsync(command);

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == "JANE@EXAMPLE.COM");

        Assert.Equal("jane@example.com", user.Email);
        Assert.Equal("Jane Doe", user.FullName);
    }

    [Fact]
    public async Task Register_DuplicateEmail_IgnoringCaseAndOrganization_IsRejectedAndCreatesNothing()
    {
        await RunAsync(NewCommand("jane@example.com", "Acme Traders"));

        // A different organization name does not make the email "new": emails are unique platform-wide.
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => RunAsync(NewCommand("JANE@Example.COM", "Globex")));

        Assert.True(exception.Errors.ContainsKey("Email"));

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // The seeded "default" organization plus Acme. The rejected sign-up left no Globex behind.
        Assert.Equal(2, await db.Organizations.CountAsync());
        Assert.False(await db.Organizations.AnyAsync(o => o.Name == "Globex"));
    }

    [Fact]
    public async Task Register_WhenAdminRoleIsMissing_ThrowsNotFound_AndCreatesNothing()
    {
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var admin = await db.Roles.SingleAsync(r => r.Name == "Admin");
            admin.Name = "Renamed";
            await db.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<NotFoundException>(() => RunAsync(NewCommand()));

        using var verifyScope = App.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Equal(1, await verifyDb.Organizations.CountAsync());   // only the seeded default
        Assert.False(await verifyDb.Users.AnyAsync(u => u.NormalizedEmail == "JANE@EXAMPLE.COM"));
    }

    [Fact]
    public async Task Register_WhileCarryingAnotherOrganizationsToken_StillChecksEveryOrganization()
    {
        var first = await RunAsync(NewCommand("first@example.com", "Acme Traders"));
        await ActAsAsync(first);   // the caller is now signed in to the first organization

        // Code uniqueness must consider ALL organizations, not only the caller's own.
        await RunAsync(NewCommand("second@example.com", "Acme Traders"));

        // The same goes for email uniqueness.
        await Assert.ThrowsAsync<ValidationException>(
            () => RunAsync(NewCommand("first@example.com", "Another Company")));

        ActAnonymously();
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var codes = await db.Organizations
            .Where(o => o.Name == "Acme Traders")
            .OrderBy(o => o.Code)
            .Select(o => o.Code)
            .ToListAsync();

        Assert.Equal(new[] { "acme-traders", "acme-traders-2" }, codes);
    }
}