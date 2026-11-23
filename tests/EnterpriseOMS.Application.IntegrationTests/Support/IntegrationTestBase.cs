using EnterpriseOMS.Application.Modules.Auth.Commands.Register;
using EnterpriseOMS.Application.Modules.Auth.DTOs;
using EnterpriseOMS.Application.Modules.Inventory.Commands.AdjustStock;
using EnterpriseOMS.Application.Modules.Inventory.Commands.IssueStock;
using EnterpriseOMS.Application.Modules.Inventory.Commands.ReceiveStock;
using EnterpriseOMS.Application.Modules.Inventory.DTOs;
using EnterpriseOMS.Application.Modules.Products.Commands.CreateProduct;
using EnterpriseOMS.Application.Modules.Products.DTOs;
using EnterpriseOMS.Application.Modules.Warehouses.Commands.CreateStorageLocation;
using EnterpriseOMS.Application.Modules.Warehouses.Commands.CreateWarehouse;
using EnterpriseOMS.Application.Modules.Warehouses.DTOs;
using EnterpriseOMS.Domain.Entities.Catalog;
using EnterpriseOMS.Domain.Entities.Warehousing;
using EnterpriseOMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseOMS.Application.IntegrationTests.Support;

/// <summary>
/// xUnit creates a new instance of the test class for every test, so each test gets
/// its own TestHost (its own fresh database). That is the isolation guarantee.
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected TestHost App { get; private set; } = default!;

    public async Task InitializeAsync() => App = await TestHost.CreateAsync();

    public async Task DisposeAsync() => await App.DisposeAsync();

    /// <summary>Registers a new organization plus its first (Admin) user, as the public sign-up does.</summary>
    protected async Task<AuthResultDto> RegisterAsync(string email, string organizationName)
    {
        using var scope = App.Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<RegisterCommandHandler>();

        return await handler.Handle(
            new RegisterCommand
            {
                FullName = "Test User",
                Email = email,
                Password = "Passw0rd1",
                OrganizationName = organizationName
            },
            CancellationToken.None);
    }

    protected async Task<int> OrganizationIdOfAsync(int userId)
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await db.Users
            .IgnoreQueryFilters()
            .Where(u => u.Id == userId)
            .Select(u => u.OrganizationId)
            .SingleAsync();
    }

    /// <summary>
    /// Creates a product in the organization of whoever is currently "signed in" (see ActAs).
    /// Goes through the real handler, so it also exercises the create rules.
    /// </summary>
    protected async Task<ProductDto> CreateProductAsync(
        string sku,
        string name = "Test Product",
        string? category = null,
        decimal unitPrice = 10m)
    {
        using var scope = App.Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<CreateProductCommandHandler>();

        return await handler.Handle(
            new CreateProductCommand
            {
                Sku = sku,
                Name = name,
                Category = category,
                UnitOfMeasure = UnitOfMeasure.Piece,
                UnitPrice = unitPrice,
                CostPrice = 1m,
                ReorderLevel = 5
            },
            CancellationToken.None);
    }

    /// <summary>Creates a warehouse in the current user's organization, through the real handler.</summary>
    protected async Task<WarehouseDto> CreateWarehouseAsync(string code, string name = "Test Warehouse", string city = "Utrecht")
    {
        using var scope = App.Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<CreateWarehouseCommandHandler>();

        return await handler.Handle(
            new CreateWarehouseCommand
            {
                Code = code,
                Name = name,
                Address = new AddressDto { Line1 = "1 Test Street", City = city, CountryCode = "NL" }
            },
            CancellationToken.None);
    }

    /// <summary>Creates a storage location in a warehouse of the current user's organization.</summary>
    protected async Task<StorageLocationDto> CreateLocationAsync(
        int warehouseId,
        string code,
        StorageLocationType type,
        int? parentId = null,
        string? name = null)
    {
        using var scope = App.Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<CreateStorageLocationCommandHandler>();

        return await handler.Handle(
            new CreateStorageLocationCommand
            {
                WarehouseId = warehouseId,
                ParentId = parentId,
                Code = code,
                Name = name,
                Type = type
            },
            CancellationToken.None);
    }

    /// <summary>Receives stock for the current user's organization, through the real handler.</summary>
    protected async Task<StockMovementDto> ReceiveStockAsync(
        int productId,
        int warehouseId,
        int quantity,
        string? reference = null,
        string? notes = null)
    {
        using var scope = App.Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<ReceiveStockCommandHandler>();

        return await handler.Handle(
            new ReceiveStockCommand
            {
                ProductId = productId,
                WarehouseId = warehouseId,
                Quantity = quantity,
                Reference = reference,
                Notes = notes
            },
            CancellationToken.None);
    }

    /// <summary>Issues stock for the current user's organization, through the real handler.</summary>
    protected async Task<StockMovementDto> IssueStockAsync(
        int productId,
        int warehouseId,
        int quantity,
        string? reference = null,
        string? notes = null)
    {
        using var scope = App.Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IssueStockCommandHandler>();

        return await handler.Handle(
            new IssueStockCommand
            {
                ProductId = productId,
                WarehouseId = warehouseId,
                Quantity = quantity,
                Reference = reference,
                Notes = notes
            },
            CancellationToken.None);
    }

    /// <summary>Records a stock count for the current user's organization, through the real handler.</summary>
    protected async Task<StockMovementDto> AdjustStockAsync(
        int productId,
        int warehouseId,
        int countedQuantity,
        string reason = "Cycle count")
    {
        using var scope = App.Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<AdjustStockCommandHandler>();

        return await handler.Handle(
            new AdjustStockCommand
            {
                ProductId = productId,
                WarehouseId = warehouseId,
                CountedQuantity = countedQuantity,
                Reason = reason
            },
            CancellationToken.None);
    }

    /// <summary>From now on, requests behave as if this user (in this organization) were signed in.</summary>
    protected void ActAs(int userId, int? organizationId)
    {
        App.CurrentUser.UserId = userId;
        App.CurrentUser.OrganizationId = organizationId;
    }

    protected async Task ActAsAsync(AuthResultDto registered) =>
        ActAs(registered.UserId, await OrganizationIdOfAsync(registered.UserId));

    /// <summary>From now on, requests behave as anonymous (login, register, seeding...).</summary>
    protected void ActAnonymously()
    {
        App.CurrentUser.UserId = null;
        App.CurrentUser.OrganizationId = null;
    }
}