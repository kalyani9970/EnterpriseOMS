using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Application.Modules.Auth.Commands.Login;
using EnterpriseOMS.Application.Modules.Auth.Commands.RefreshTokens;
using EnterpriseOMS.Application.Modules.Auth.Commands.Register;
using EnterpriseOMS.Infrastructure.Identity;
using EnterpriseOMS.Infrastructure.Persistence;
using EnterpriseOMS.Infrastructure.Persistence.Seed;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.IntegrationTests.Support
{
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
                options.OrganizationId = 1;
            });

            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
            services.AddScoped<IdentitySeeder>();

            // Handlers are resolved by concrete type: we test our logic, not the MediatR pipeline.
            services.AddScoped<RegisterCommandHandler>();
            services.AddScoped<LoginCommandHandler>();
            services.AddScoped<RefreshTokenCommandHandler>();

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
}
