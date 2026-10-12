using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Application.Common.Security;
using EnterpriseOMS.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Infrastructure.Persistence.Seed
{
    /// <summary>
    /// Idempotent seeder: safe to run on every startup.
    /// 1. Permissions are discovered by reflecting over the nested classes of the
    ///    Permissions constants class, so adding a constant there is enough. The next
    ///    startup inserts it and grants it to Admin automatically.
    /// 2. System roles: Admin (all permissions) and Employee (baseline, no permissions yet).
    /// 3. An optional bootstrap admin user, from configuration.
    /// </summary>
    public class IdentitySeeder
    {
        public const string AdminRoleName = "Admin";
        public const string EmployeeRoleName = "Employee";

        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher _passwordHasher;
        private readonly SeedAdminSettings _adminSettings;
        private readonly ILogger<IdentitySeeder> _logger;

        public IdentitySeeder(
            ApplicationDbContext context,
            IPasswordHasher passwordHasher,
            IOptions<SeedAdminSettings> adminOptions,
            ILogger<IdentitySeeder> logger)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _adminSettings = adminOptions.Value;
            _logger = logger;
        }

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            await SeedPermissionsAsync(cancellationToken);
            await SeedRolesAsync(cancellationToken);
            await SeedAdminUserAsync(cancellationToken);
        }

        private async Task SeedPermissionsAsync(CancellationToken ct)
        {
            var discovered = DiscoverPermissions();

            var existingCodes = await _context.Permissions
                .Select(p => p.Code)
                .ToListAsync(ct);

            var newPermissions = discovered
                .Where(d => !existingCodes.Contains(d.Code))
                .Select(d => new Permission
                {
                    Code = d.Code,
                    Module = d.Module,
                    Description = $"Permission '{d.Code}'"
                })
                .ToList();

            if (newPermissions.Count == 0) return;

            _context.Permissions.AddRange(newPermissions);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Seeded {Count} new permission(s).", newPermissions.Count);
        }

        private async Task SeedRolesAsync(CancellationToken ct)
        {
            var allPermissions = await _context.Permissions.ToListAsync(ct);

            // --- Admin: always holds every permission, including ones added later ---
            var admin = await _context.Roles
                .Include(r => r.RolePermissions)
                .FirstOrDefaultAsync(r => r.Name == AdminRoleName, ct);

            if (admin is null)
            {
                admin = new Role
                {
                    Name = AdminRoleName,
                    Description = "Full access to all modules.",
                    IsSystemRole = true
                };
                _context.Roles.Add(admin);
            }

            foreach (var permission in allPermissions)
            {
                admin.AssignPermission(permission);   // no-op if already assigned
            }

            // --- Employee: baseline role for self-registration ---
            var employeeExists = await _context.Roles.AnyAsync(r => r.Name == EmployeeRoleName, ct);
            if (!employeeExists)
            {
                _context.Roles.Add(new Role
                {
                    Name = EmployeeRoleName,
                    Description = "Baseline role. Permissions are granted per module as they are built.",
                    IsSystemRole = true
                });
            }

            await _context.SaveChangesAsync(ct);
        }

        private async Task SeedAdminUserAsync(CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_adminSettings.Email) ||
                string.IsNullOrWhiteSpace(_adminSettings.Password))
            {
                _logger.LogWarning("SeedAdmin:Email/Password not configured. Skipping admin user seeding.");
                return;
            }

            var normalizedEmail = _adminSettings.Email.Trim().ToUpperInvariant();

            var exists = await _context.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, ct);
            if (exists) return;

            var adminRole = await _context.Roles.FirstAsync(r => r.Name == AdminRoleName, ct);

            _context.Users.Add(new User
            {
                FullName = _adminSettings.FullName,
                Email = _adminSettings.Email.Trim(),
                NormalizedEmail = normalizedEmail,
                PasswordHash = _passwordHasher.Hash(_adminSettings.Password),
                OrganizationId = _adminSettings.OrganizationId,
                RoleId = adminRole.Id,
                IsActive = true
            });

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Seeded bootstrap admin user {Email}.", _adminSettings.Email);
        }

        /// <summary>
        /// Reflection over Permissions.{Module}.{Constant}. The nested class name becomes
        /// the module name, and each string constant becomes a permission code.
        /// </summary>
        private static List<(string Module, string Code)> DiscoverPermissions()
        {
            var result = new List<(string Module, string Code)>();

            foreach (var moduleType in typeof(Permissions).GetNestedTypes(BindingFlags.Public))
            {
                var constants = moduleType
                    .GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string));

                foreach (var field in constants)
                {
                    var code = (string?)field.GetRawConstantValue();
                    if (!string.IsNullOrWhiteSpace(code))
                    {
                        result.Add((moduleType.Name, code));
                    }
                }
            }

            return result;
        }
    }
}
