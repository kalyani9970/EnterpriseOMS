using EnterpriseOMS.Application.Common.Exceptions;
using EnterpriseOMS.Application.IntegrationTests.Support;
using EnterpriseOMS.Application.Modules.Auth.DTOs;
using EnterpriseOMS.Application.Modules.Warehouses.Commands.CreateWarehouse;
using EnterpriseOMS.Application.Modules.Warehouses.Commands.SetWarehouseActive;
using EnterpriseOMS.Application.Modules.Warehouses.Commands.UpdateWarehouse;
using EnterpriseOMS.Application.Modules.Warehouses.DTOs;
using EnterpriseOMS.Domain.Common.Exceptions;
using EnterpriseOMS.Domain.Entities.Warehousing;
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
    public class WarehouseCommandHandlerTests : IntegrationTestBase
    {
        // ---------- helpers ----------

        private static CreateWarehouseCommand NewWarehouse(string code = "WH-MAIN") => new()
        {
            Code = code,
            Name = "Main Distribution Center",
            Address = new AddressDto
            {
                Line1 = "100 Commerce Way",
                Line2 = "Gate 4",
                City = "Rotterdam",
                Region = "South Holland",
                PostalCode = "3011 AA",
                CountryCode = "NL"
            }
        };

        private async Task<AuthResultDto> SignInAsync(string email = "alice@acme.test", string organization = "Acme Traders")
        {
            var registered = await RegisterAsync(email, organization);
            await ActAsAsync(registered);
            return registered;
        }

        private async Task<WarehouseDto> CreateAsync(CreateWarehouseCommand command)
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<CreateWarehouseCommandHandler>();

            return await handler.Handle(command, CancellationToken.None);
        }

        /// <summary>Builds an update from the warehouse as the caller last saw it, then lets the test change fields.</summary>
        private async Task<WarehouseDto> UpdateAsync(WarehouseDto current, Action<UpdateWarehouseCommand>? change = null)
        {
            var command = new UpdateWarehouseCommand
            {
                Id = current.Id,
                Version = current.Version,
                Name = current.Name,
                Address = new AddressDto
                {
                    Line1 = current.Address.Line1,
                    Line2 = current.Address.Line2,
                    City = current.Address.City,
                    Region = current.Address.Region,
                    PostalCode = current.Address.PostalCode,
                    CountryCode = current.Address.CountryCode
                }
            };
            change?.Invoke(command);

            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<UpdateWarehouseCommandHandler>();

            return await handler.Handle(command, CancellationToken.None);
        }

        private async Task<WarehouseDto> SetActiveAsync(int id, Guid version, bool isActive)
        {
            using var scope = App.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<SetWarehouseActiveCommandHandler>();

            return await handler.Handle(
                new SetWarehouseActiveCommand { Id = id, Version = version, IsActive = isActive },
                CancellationToken.None);
        }

        /// <summary>Reads the stored row, ignoring tenant filters, so tests can check what really is in the database.</summary>
        private async Task<Warehouse> LoadAsync(int id)
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            return await db.Warehouses.AsNoTracking().IgnoreQueryFilters().SingleAsync(w => w.Id == id);
        }

        private async Task<int> CountWarehousesAsync()
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            return await db.Warehouses.IgnoreQueryFilters().CountAsync();
        }

        // ---------- create ----------

        [Fact]
        public async Task Create_StoresTheWarehouseInTheCallersOrganization()
        {
            var alice = await SignInAsync();
            var organizationId = await OrganizationIdOfAsync(alice.UserId);

            var created = await CreateAsync(NewWarehouse());

            var stored = await LoadAsync(created.Id);
            Assert.Equal(organizationId, stored.OrganizationId);
            Assert.Equal("WH-MAIN", stored.Code);
            Assert.Equal("Main Distribution Center", stored.Name);
        }

        [Fact]
        public async Task Create_ReturnsTheFullDetails_IncludingTheNestedAddressAndVersion()
        {
            await SignInAsync();

            var created = await CreateAsync(NewWarehouse());

            Assert.True(created.Id > 0);
            Assert.Equal("WH-MAIN", created.Code);
            Assert.Equal("Main Distribution Center", created.Name);
            Assert.Equal("100 Commerce Way", created.Address.Line1);
            Assert.Equal("Gate 4", created.Address.Line2);
            Assert.Equal("Rotterdam", created.Address.City);
            Assert.Equal("South Holland", created.Address.Region);
            Assert.Equal("3011 AA", created.Address.PostalCode);
            Assert.Equal("NL", created.Address.CountryCode);
            Assert.True(created.IsActive);
            Assert.Equal(0, created.LocationCount);
            Assert.NotEqual(Guid.Empty, created.Version);
            Assert.Equal(App.Clock.UtcNow, created.Created);
            Assert.Null(created.LastModified);
        }

        [Fact]
        public async Task Create_PersistsTheAddressInsideTheWarehouseRow()
        {
            await SignInAsync();
            var command = NewWarehouse();
            command.Address.CountryCode = " nl ";
            command.Address.Line2 = "   ";

            var created = await CreateAsync(command);

            var stored = await LoadAsync(created.Id);
            Assert.Equal("NL", stored.Address.CountryCode);   // normalized by the value object
            Assert.Null(stored.Address.Line2);                // blank optional field stored as null
            Assert.Equal("Rotterdam", stored.Address.City);
        }

        [Theory]
        [InlineData("WH-MAIN")]
        [InlineData("wh-main")]
        [InlineData("  Wh-Main  ")]
        public async Task Create_DuplicateCode_IsRejected_AndSavesNothingMore(string duplicateCode)
        {
            await SignInAsync();
            await CreateAsync(NewWarehouse("WH-MAIN"));

            var exception = await Assert.ThrowsAsync<ValidationException>(() => CreateAsync(NewWarehouse(duplicateCode)));

            Assert.Equal("A warehouse with this code already exists.", Assert.Single(exception.Errors["Code"]));
            Assert.Equal(1, await CountWarehousesAsync());
        }

        [Fact]
        public async Task Create_TheSameCodeInAnotherOrganization_IsAllowed()
        {
            await SignInAsync("alice@acme.test", "Acme Traders");
            await CreateAsync(NewWarehouse("WH-MAIN"));

            await SignInAsync("bob@globex.test", "Globex");
            await CreateAsync(NewWarehouse("WH-MAIN"));

            Assert.Equal(2, await CountWarehousesAsync());
        }

        [Fact]
        public async Task Create_WithNoOrganizationInTheToken_ThrowsForbidden()
        {
            ActAs(userId: 1, organizationId: null);

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateAsync(NewWarehouse()));
        }

        [Fact]
        public async Task Create_WithABlankName_ThrowsADomainException_AndSavesNothing()
        {
            await SignInAsync();
            var command = NewWarehouse();
            command.Name = "   ";   // the validators would stop this earlier; the entity must refuse it too

            await Assert.ThrowsAsync<DomainException>(() => CreateAsync(command));

            Assert.Equal(0, await CountWarehousesAsync());
        }

        [Fact]
        public async Task Create_WithAnInvalidCountryCode_ThrowsADomainException_AndSavesNothing()
        {
            await SignInAsync();
            var command = NewWarehouse();
            command.Address.CountryCode = "NLD";

            await Assert.ThrowsAsync<DomainException>(() => CreateAsync(command));

            Assert.Equal(0, await CountWarehousesAsync());
        }

        [Fact]
        public async Task Create_RecordsWhoCreatedIt()
        {
            var alice = await SignInAsync();

            var created = await CreateAsync(NewWarehouse());

            Assert.Equal(alice.UserId.ToString(), (await LoadAsync(created.Id)).CreatedBy);
        }

        // ---------- update ----------

        [Fact]
        public async Task Update_ChangesTheNameAndAddress_AndIssuesANewVersion()
        {
            await SignInAsync();
            var created = await CreateAsync(NewWarehouse());

            var updated = await UpdateAsync(created, c =>
            {
                c.Name = "  East Hub  ";
                c.Address.City = "Antwerp";
                c.Address.CountryCode = "be";
            });

            Assert.Equal("East Hub", updated.Name);
            Assert.Equal("Antwerp", updated.Address.City);
            Assert.Equal("BE", updated.Address.CountryCode);
            Assert.NotEqual(created.Version, updated.Version);

            var stored = await LoadAsync(created.Id);
            Assert.Equal("East Hub", stored.Name);
            Assert.Equal("Antwerp", stored.Address.City);
            Assert.Equal(updated.Version, stored.Version);
        }

        [Fact]
        public async Task Update_NeverChangesTheCode()
        {
            await SignInAsync();
            var created = await CreateAsync(NewWarehouse("wh-main"));

            await UpdateAsync(created, c => c.Name = "Renamed");

            var stored = await LoadAsync(created.Id);
            Assert.Equal("wh-main", stored.Code);
            Assert.Equal("WH-MAIN", stored.NormalizedCode);
        }

        [Fact]
        public async Task Update_WithAStaleVersion_IsRejected_AndChangesNothing()
        {
            await SignInAsync();
            var created = await CreateAsync(NewWarehouse());

            var first = await UpdateAsync(created, c => c.Name = "First edit");

            // The second editor still holds the ORIGINAL copy, so their edit is based on old data.
            var exception = await Assert.ThrowsAsync<ConcurrencyException>(
                () => UpdateAsync(created, c => c.Name = "Second edit, based on the old copy"));

            Assert.Contains("was changed by someone else", exception.Message);

            var stored = await LoadAsync(created.Id);
            Assert.Equal("First edit", stored.Name);
            Assert.Equal(first.Version, stored.Version);
        }

        [Fact]
        public async Task Update_WithTheLatestVersion_CanBeRepeated()
        {
            await SignInAsync();
            var created = await CreateAsync(NewWarehouse());

            var first = await UpdateAsync(created, c => c.Name = "First");
            var second = await UpdateAsync(first, c => c.Name = "Second");

            Assert.Equal("Second", second.Name);
            Assert.NotEqual(first.Version, second.Version);
        }

        [Fact]
        public async Task Update_UnknownWarehouse_ThrowsNotFound()
        {
            await SignInAsync();
            var ghost = new WarehouseDto
            {
                Id = 9999,
                Version = Guid.NewGuid(),
                Name = "Ghost",
                Address = new AddressDto { Line1 = "1 St", City = "X", CountryCode = "NL" }
            };

            await Assert.ThrowsAsync<NotFoundException>(() => UpdateAsync(ghost));
        }

        [Fact]
        public async Task Update_AWarehouseOfAnotherOrganization_IsNotFound_AndStaysUntouched()
        {
            await SignInAsync("alice@acme.test", "Acme Traders");
            var alicesWarehouse = await CreateAsync(NewWarehouse());

            await SignInAsync("bob@globex.test", "Globex");

            await Assert.ThrowsAsync<NotFoundException>(() => UpdateAsync(alicesWarehouse, c => c.Name = "Hacked"));

            var stored = await LoadAsync(alicesWarehouse.Id);
            Assert.Equal("Main Distribution Center", stored.Name);
            Assert.Equal(alicesWarehouse.Version, stored.Version);
        }

        [Fact]
        public async Task Update_InvalidData_ThrowsADomainException_AndKeepsTheStoredValues()
        {
            await SignInAsync();
            var created = await CreateAsync(NewWarehouse());

            await Assert.ThrowsAsync<DomainException>(() => UpdateAsync(created, c => c.Address.City = "  "));

            var stored = await LoadAsync(created.Id);
            Assert.Equal("Rotterdam", stored.Address.City);
            Assert.Equal(created.Version, stored.Version);
        }

        [Fact]
        public async Task Update_StampsLastModified_AndWhoDidIt()
        {
            var alice = await SignInAsync();
            var created = await CreateAsync(NewWarehouse());
            App.Clock.Advance(TimeSpan.FromMinutes(5));
            var editedAt = App.Clock.UtcNow;

            await UpdateAsync(created, c => c.Name = "Edited");

            var stored = await LoadAsync(created.Id);
            Assert.Equal(editedAt, stored.LastModified);
            Assert.Equal(alice.UserId.ToString(), stored.LastModifiedBy);
            Assert.Equal(created.Created, stored.Created);
        }

        [Fact]
        public async Task Update_ReturnsTheCurrentLocationCount()
        {
            await SignInAsync();
            var created = await CreateAsync(NewWarehouse());
            await CreateLocationAsync(created.Id, "A", StorageLocationType.Zone);
            await CreateLocationAsync(created.Id, "B", StorageLocationType.Zone);

            var updated = await UpdateAsync(created, c => c.Name = "Renamed");

            Assert.Equal(2, updated.LocationCount);
        }

        // ---------- activate / deactivate ----------

        [Fact]
        public async Task Deactivate_FlipsTheFlag_AndIssuesANewVersion()
        {
            await SignInAsync();
            var created = await CreateAsync(NewWarehouse());

            var deactivated = await SetActiveAsync(created.Id, created.Version, isActive: false);

            Assert.False(deactivated.IsActive);
            Assert.NotEqual(created.Version, deactivated.Version);

            var stored = await LoadAsync(created.Id);
            Assert.False(stored.IsActive);
            Assert.Equal(deactivated.Version, stored.Version);
        }

        [Fact]
        public async Task Reactivate_BringsAnInactiveWarehouseBack()
        {
            await SignInAsync();
            var created = await CreateAsync(NewWarehouse());
            var deactivated = await SetActiveAsync(created.Id, created.Version, isActive: false);

            var reactivated = await SetActiveAsync(created.Id, deactivated.Version, isActive: true);

            Assert.True(reactivated.IsActive);
            Assert.True((await LoadAsync(created.Id)).IsActive);
        }

        [Fact]
        public async Task Deactivate_WhenAlreadyInactive_KeepsTheVersion()
        {
            await SignInAsync();
            var created = await CreateAsync(NewWarehouse());
            var deactivated = await SetActiveAsync(created.Id, created.Version, isActive: false);

            var again = await SetActiveAsync(created.Id, deactivated.Version, isActive: false);

            Assert.False(again.IsActive);
            Assert.Equal(deactivated.Version, again.Version);
        }

        [Fact]
        public async Task Deactivate_WithAStaleVersion_IsRejected_AndStaysActive()
        {
            await SignInAsync();
            var created = await CreateAsync(NewWarehouse());
            await UpdateAsync(created, c => c.Name = "Someone else edited it first");

            await Assert.ThrowsAsync<ConcurrencyException>(
                () => SetActiveAsync(created.Id, created.Version, isActive: false));

            Assert.True((await LoadAsync(created.Id)).IsActive);
        }

        [Fact]
        public async Task Deactivate_UnknownWarehouse_ThrowsNotFound()
        {
            await SignInAsync();

            await Assert.ThrowsAsync<NotFoundException>(
                () => SetActiveAsync(9999, Guid.NewGuid(), isActive: false));
        }

        [Fact]
        public async Task Deactivate_AWarehouseOfAnotherOrganization_IsNotFound_AndStaysActive()
        {
            await SignInAsync("alice@acme.test", "Acme Traders");
            var alicesWarehouse = await CreateAsync(NewWarehouse());

            await SignInAsync("bob@globex.test", "Globex");

            await Assert.ThrowsAsync<NotFoundException>(
                () => SetActiveAsync(alicesWarehouse.Id, alicesWarehouse.Version, isActive: false));

            Assert.True((await LoadAsync(alicesWarehouse.Id)).IsActive);
        }
    }
}
