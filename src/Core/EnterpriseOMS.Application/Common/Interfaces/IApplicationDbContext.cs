using EnterpriseOMS.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Common.Interfaces
/// <summary>
/// Abstraction over the EF Core DbContext exposed to the Application layer.
/// Query handlers depend on THIS interface, not on EnterpriseOMS.Infrastructure's
/// concrete ApplicationDbContext — that's what keeps Application ignorant of
/// Infrastructure (dependency inversion). As we add modules, each entity's
/// DbSet gets added here too.
/// </summary>
{
    public interface IApplicationDbContext
    {
        DbSet<User> Users { get; }
        DbSet<Role> Roles { get; }
        DbSet<Permission> Permissions { get; }
        DbSet<RolePermission> RolePermissions { get; }
        DbSet<RefreshToken> RefreshTokens { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}
