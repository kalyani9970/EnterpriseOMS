using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Infrastructure.Authorization;
using EnterpriseOMS.Infrastructure.Identity;
using EnterpriseOMS.Infrastructure.Persistence;
using EnterpriseOMS.Infrastructure.Persistence.Seed;
using EnterpriseOMS.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
        {
            // --- Persistence ---
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(
                    connectionString,
                    sql => sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

            // Application depends on the interface; resolve it to the same scoped DbContext instance
            // so handlers and the seeder share one unit of work per request.
            services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

            // --- Options ---
            services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
            services.Configure<SeedAdminSettings>(configuration.GetSection(SeedAdminSettings.SectionName));

            // --- Services ---
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddSingleton<IDateTime, DateTimeService>();
            services.AddSingleton<IPasswordHasher, PasswordHasherService>();
            services.AddSingleton<IJwtTokenService, JwtTokenService>();
            services.AddScoped<IdentitySeeder>();

            // --- Authorization (permission-based RBAC) ---
            services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
            services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

            return services;
        }
    }
}
