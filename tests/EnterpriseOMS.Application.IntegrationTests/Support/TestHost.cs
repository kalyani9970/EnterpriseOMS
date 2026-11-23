using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Application.Modules.Auth.Commands.Login;
using EnterpriseOMS.Application.Modules.Auth.Commands.RefreshTokens;
using EnterpriseOMS.Application.Modules.Auth.Commands.Register;
using EnterpriseOMS.Application.Modules.Inventory.Commands.AdjustStock;
using EnterpriseOMS.Application.Modules.Inventory.Commands.IssueStock;
using EnterpriseOMS.Application.Modules.Inventory.Commands.ReceiveStock;
using EnterpriseOMS.Application.Modules.Inventory.Queries.GetStockLevels;
using EnterpriseOMS.Application.Modules.Inventory.Queries.GetStockMovements;
using EnterpriseOMS.Application.Modules.Organizations.Commands.UpdateMyOrganization;
using EnterpriseOMS.Application.Modules.Organizations.Queries.GetMyOrganization;
using EnterpriseOMS.Application.Modules.Products.Commands.CreateProduct;
using EnterpriseOMS.Application.Modules.Products.Commands.SetProductActive;
using EnterpriseOMS.Application.Modules.Products.Commands.UpdateProduct;
using EnterpriseOMS.Application.Modules.Products.Queries.GetProductById;
using EnterpriseOMS.Application.Modules.Products.Queries.GetProductCategories;
using EnterpriseOMS.Application.Modules.Products.Queries.GetProductsList;
using EnterpriseOMS.Application.Modules.Warehouses.Commands.CreateStorageLocation;
using EnterpriseOMS.Application.Modules.Warehouses.Commands.CreateWarehouse;
using EnterpriseOMS.Application.Modules.Warehouses.Commands.SetStorageLocationActive;
using EnterpriseOMS.Application.Modules.Warehouses.Commands.SetWarehouseActive;
using EnterpriseOMS.Application.Modules.Warehouses.Commands.UpdateStorageLocation;
using EnterpriseOMS.Application.Modules.Warehouses.Commands.UpdateWarehouse;
using EnterpriseOMS.Application.Modules.Warehouses.Queries.GetWarehouseById;
using EnterpriseOMS.Application.Modules.Warehouses.Queries.GetWarehouseLocations;
using EnterpriseOMS.Application.Modules.Warehouses.Queries.GetWarehousesList;
using EnterpriseOMS.Infrastructure.Identity;
using EnterpriseOMS.Infrastructure.Persistence;
using EnterpriseOMS.Infrastructure.Persistence.Seed;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseOMS.Application.IntegrationTests.Support;

/// <summary>
/// A tiny composition root for tests: real DbContext on SQLite in-memory, real hasher,
/// real JWT service, real seeder, with a fake clock and fake current user.
/// The SQLite in-memory database lives only as long as its connection, so we open one
/// connection and keep it for the host's lifetime. All scopes share that one database.
/// </summary>
public sealed class TestHost : IAsyncDisposable
{
    public const string AdminEmail = "admin@test.local";
    public const string AdminPassword = "Admin@12345";

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public IServiceProvider Services => _provider;
    public FakeDateTime Clock { get; }
    public TestCurrentUser CurrentUser { get; }

    private TestHost(SqliteConnection connection, ServiceProvider provider, FakeDateTime clock, TestCurrentUser currentUser)
    {
        _connection = connection;
        _provider = provider;
        Clock = clock;
        CurrentUser = currentUser;
    }

    public static async Task<TestHost> CreateAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var clock = new FakeDateTime();
        var currentUser = new TestCurrentUser();

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSingleton<IDateTime>(clock);
        services.AddSingleton(clock);
        services.AddSingleton(currentUser);
        services.AddSingleton<ICurrentUserService>(currentUser);

        services.AddSingleton<IPasswordHasher, PasswordHasherService>();
        services.Configure<JwtSettings>(options =>
        {
            options.Secret = new string('k', 64);
            options.Issuer = "test-issuer";
            options.Audience = "test-audience";
            options.AccessTokenExpiryMinutes = 15;
        });
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        services.Configure<SeedAdminSettings>(options =>
        {
            options.FullName = "Test Admin";
            options.Email = AdminEmail;
            options.Password = AdminPassword;
        });
        services.Configure<SeedDemoDataSettings>(options =>
        {
            options.Products = true;
            options.Warehouses = true;
            options.Inventory = true;
        });

        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IdentitySeeder>();
        services.AddScoped<ProductDemoSeeder>();
        services.AddScoped<WarehouseDemoSeeder>();
        services.AddScoped<InventoryDemoSeeder>();

        // Handlers are resolved by concrete type: we test our logic, not the MediatR pipeline.
        services.AddScoped<RegisterCommandHandler>();
        services.AddScoped<LoginCommandHandler>();
        services.AddScoped<RefreshTokenCommandHandler>();
        services.AddScoped<GetMyOrganizationQueryHandler>();
        services.AddScoped<UpdateMyOrganizationCommandHandler>();

        services.AddScoped<CreateProductCommandHandler>();
        services.AddScoped<UpdateProductCommandHandler>();
        services.AddScoped<SetProductActiveCommandHandler>();
        services.AddScoped<GetProductByIdQueryHandler>();
        services.AddScoped<GetProductsListQueryHandler>();
        services.AddScoped<GetProductCategoriesQueryHandler>();

        services.AddScoped<CreateWarehouseCommandHandler>();
        services.AddScoped<UpdateWarehouseCommandHandler>();
        services.AddScoped<SetWarehouseActiveCommandHandler>();
        services.AddScoped<CreateStorageLocationCommandHandler>();
        services.AddScoped<UpdateStorageLocationCommandHandler>();
        services.AddScoped<SetStorageLocationActiveCommandHandler>();
        services.AddScoped<GetWarehouseByIdQueryHandler>();
        services.AddScoped<GetWarehousesListQueryHandler>();
        services.AddScoped<GetWarehouseLocationsQueryHandler>();

        services.AddScoped<ReceiveStockCommandHandler>();
        services.AddScoped<IssueStockCommandHandler>();
        services.AddScoped<AdjustStockCommandHandler>();
        services.AddScoped<GetStockLevelsQueryHandler>();
        services.AddScoped<GetStockMovementsQueryHandler>();

        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.EnsureCreatedAsync();

            await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();
        }

        return new TestHost(connection, provider, clock, currentUser);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }
}