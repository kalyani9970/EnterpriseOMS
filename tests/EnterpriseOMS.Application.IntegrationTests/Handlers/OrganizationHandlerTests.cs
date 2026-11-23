using EnterpriseOMS.Application.Common.Exceptions;
using EnterpriseOMS.Application.IntegrationTests.Support;
using EnterpriseOMS.Application.Modules.Organizations.Commands.UpdateMyOrganization;
using EnterpriseOMS.Application.Modules.Organizations.DTOs;
using EnterpriseOMS.Application.Modules.Organizations.Queries.GetMyOrganization;
using EnterpriseOMS.Domain.Common.Exceptions;
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
    public class OrganizationHandlerTests : IntegrationTestBase
    {
        private async Task<OrganizationDto> GetMineAsync()
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<GetMyOrganizationQueryHandler>();

            return await handler.Handle(new GetMyOrganizationQuery(), CancellationToken.None);
        }

        private async Task<OrganizationDto> RenameAsync(string name)
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<UpdateMyOrganizationCommandHandler>();

            return await handler.Handle(new UpdateMyOrganizationCommand { Name = name }, CancellationToken.None);
        }

        private async Task<string> NameOfOrganizationAsync(string code)
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var organization = await db.Organizations.IgnoreQueryFilters().SingleAsync(o => o.Code == code);
            return organization.Name;
        }

        [Fact]
        public async Task GetMine_ReturnsTheCallersOrganizationWithItsUserCount()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            await RegisterAsync("bob@globex.test", "Globex");
            await ActAsAsync(alice);

            var organization = await GetMineAsync();

            Assert.Equal("Acme Traders", organization.Name);
            Assert.Equal("acme-traders", organization.Code);
            Assert.True(organization.IsActive);
            Assert.Equal(1, organization.UserCount);
            Assert.Equal(App.Clock.UtcNow, organization.Created);
        }

        [Fact]
        public async Task GetMine_FollowsWhoIsSignedIn()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            var bob = await RegisterAsync("bob@globex.test", "Globex");

            await ActAsAsync(alice);
            Assert.Equal("Acme Traders", (await GetMineAsync()).Name);

            await ActAsAsync(bob);
            Assert.Equal("Globex", (await GetMineAsync()).Name);
        }

        [Fact]
        public async Task GetMine_WithNoOrganizationInTheToken_ThrowsForbidden()
        {
            ActAs(userId: 1, organizationId: null);

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => GetMineAsync());
        }

        [Fact]
        public async Task Update_RenamesAndTrims_ButKeepsTheCode()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            await ActAsAsync(alice);

            var updated = await RenameAsync("  Acme Holdings  ");

            Assert.Equal("Acme Holdings", updated.Name);
            Assert.Equal("acme-traders", updated.Code);
            Assert.Equal(1, updated.UserCount);
        }

        [Fact]
        public async Task Update_PersistsTheNewName()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            await ActAsAsync(alice);

            await RenameAsync("Acme Holdings");

            Assert.Equal("Acme Holdings", (await GetMineAsync()).Name);
        }

        [Fact]
        public async Task Update_OnlyChangesTheCallersOwnOrganization()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            await RegisterAsync("bob@globex.test", "Globex");
            await ActAsAsync(alice);

            await RenameAsync("Acme Holdings");

            Assert.Equal("Acme Holdings", await NameOfOrganizationAsync("acme-traders"));
            Assert.Equal("Globex", await NameOfOrganizationAsync("globex"));
        }

        [Fact]
        public async Task Update_InvalidName_ThrowsDomainException_AndKeepsTheOldName()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            await ActAsAsync(alice);

            await Assert.ThrowsAsync<DomainException>(() => RenameAsync("   "));

            Assert.Equal("Acme Traders", (await GetMineAsync()).Name);
        }

        [Fact]
        public async Task Update_WithNoOrganizationInTheToken_ThrowsForbidden()
        {
            ActAs(userId: 1, organizationId: null);

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => RenameAsync("Anything"));
        }

        [Fact]
        public async Task Update_StampsLastModifiedAndWhoDidIt()
        {
            var alice = await RegisterAsync("alice@acme.test", "Acme Traders");
            await ActAsAsync(alice);
            App.Clock.Advance(TimeSpan.FromMinutes(5));
            var renamedAt = App.Clock.UtcNow;

            await RenameAsync("Acme Holdings");

            ActAnonymously();
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var organization = await db.Organizations.SingleAsync(o => o.Code == "acme-traders");

            Assert.Equal(renamedAt, organization.LastModified);
            Assert.Equal(alice.UserId.ToString(), organization.LastModifiedBy);
        }
    }
}
