using EnterpriseOMS.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Infrastructure.Persistence
{
    public static class DatabaseInitialiser
    {
        /// <summary>
        /// Applies pending migrations and runs the seeder. Called once from Program.cs at startup.
        /// A scope is created manually because this runs outside any HTTP request,
        /// and DbContext is a Scoped service.
        /// Applying migrations at startup is convenient for development. In production you would
        /// typically run migrations as a separate deployment step instead.
        /// </summary>
        public static async Task InitialiseDatabaseAsync(this IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.MigrateAsync();

            var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
            await seeder.SeedAsync();
        }
    }
}
