using System.Reflection;
using EnterpriseOMS.Application.Common.Exceptions;
using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Domain.Common;
using EnterpriseOMS.Domain.Entities.Catalog;
using EnterpriseOMS.Domain.Entities.Identity;
using EnterpriseOMS.Domain.Entities.Inventory;
using EnterpriseOMS.Domain.Entities.Tenancy;
using EnterpriseOMS.Domain.Entities.Warehousing;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseOMS.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext, IUnitOfWork
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserService currentUserService,
        IDateTime dateTime) : base(options)
    {
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<StorageLocation> StorageLocations => Set<StorageLocation>();
    public DbSet<StockLevel> StockLevels => Set<StockLevel>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // ---- Read by the global query filters below. ----
    // EF Core re-evaluates these members on the context instance that is EXECUTING each query, so
    // every request is filtered by its own user, even though the model is built only once.

    /// <summary>
    /// True for anonymous work: login, register, refresh, logout and database seeding. The user's
    /// organization is not known yet, so tenant filtering cannot apply. A signed-in user is never
    /// bypassed: a token without an organization sees nothing (it fails closed).
    /// </summary>
    public bool IsTenantFilterBypassed => !_currentUserService.IsAuthenticated;

    public int? CurrentOrganizationId => _currentUserService.OrganizationId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Domain events live in memory on BaseEntity and must never be persisted.
        modelBuilder.Ignore<BaseDomainEvent>();

        // Picks up every IEntityTypeConfiguration<T> in this assembly automatically,
        // so new modules just add a configuration class and need no edits here.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        ApplyTenantFilters(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        // Every entity that implements ITenantEntity (User, Product, Warehouse, StorageLocation, StockLevel,
        // StockMovement and every future business entity) gets the filter automatically. New modules only
        // implement the interface.
        var applyFilter = typeof(ApplicationDbContext)
            .GetMethod(nameof(ApplyTenantFilter), BindingFlags.Instance | BindingFlags.NonPublic)!;

        var tenantEntityTypes = modelBuilder.Model.GetEntityTypes()
            .Where(t => typeof(ITenantEntity).IsAssignableFrom(t.ClrType))
            .Select(t => t.ClrType)
            .ToList();

        foreach (var clrType in tenantEntityTypes)
        {
            applyFilter.MakeGenericMethod(clrType).Invoke(this, new object[] { modelBuilder });
        }

        // Organization is the tenant root: a user may only see their own.
        modelBuilder.Entity<Organization>().HasQueryFilter(o =>
            IsTenantFilterBypassed || o.Id == CurrentOrganizationId);

        // RefreshToken has no OrganizationId of its own: it belongs to a user, who belongs to an
        // organization. Filtering it the same way also keeps its required link to User consistent.
        modelBuilder.Entity<RefreshToken>().HasQueryFilter(rt =>
            IsTenantFilterBypassed || rt.User.OrganizationId == CurrentOrganizationId);
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, ITenantEntity
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
            IsTenantFilterBypassed ||
            EF.Property<int>(e, nameof(ITenantEntity.OrganizationId)) == CurrentOrganizationId);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnforceLedgerIsAppendOnly();
        EnforceTenantBoundaryOnWrites();
        StampAuditFields();

        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The stock ledger is an audit trail: a line, once written, is never changed or removed. A mistake is
    /// corrected by writing a NEW line (an adjustment), not by editing history. This guard makes that a rule
    /// of the system rather than a habit of the code: even a buggy handler cannot rewrite the ledger.
    /// </summary>
    private void EnforceLedgerIsAppendOnly()
    {
        foreach (var entry in ChangeTracker.Entries<StockMovement>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "Stock movements are append-only: a ledger line can never be changed or deleted.");
            }
        }
    }

    /// <summary>
    /// The read filter hides other organizations' rows. This is its write-side twin: a signed-in user
    /// can never SAVE a tenant row for another organization, or move an existing row to another one.
    /// It is a safety net behind the handlers (which already set the organization from the token), so
    /// even a buggy handler cannot write across the tenant boundary.
    /// </summary>
    private void EnforceTenantBoundaryOnWrites()
    {
        // Anonymous work (sign-up, login, seeding) happens before an organization is known.
        if (!_currentUserService.IsAuthenticated) return;

        var organizationId = _currentUserService.OrganizationId;

        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    {
                        // A value of 0 or below means "not assigned yet" or the temporary key of an organization
                        // created in the same save (sign-up). The foreign key decides those. A real, positive
                        // organization that is not the caller's is a violation.
                        var assigned = entry.Entity.OrganizationId;
                        if (assigned > 0 && assigned != organizationId)
                            throw new ForbiddenAccessException();
                        break;
                    }

                case EntityState.Modified when entry.Property(nameof(ITenantEntity.OrganizationId)).IsModified:
                    throw new ForbiddenAccessException();
            }
        }
    }

    /// <summary>
    /// Stamps audit fields automatically. EF's ChangeTracker knows which entities are
    /// Added or Modified at this moment, so handlers never set Created/LastModified.
    /// </summary>
    private void StampAuditFields()
    {
        var now = _dateTime.UtcNow;
        var userId = _currentUserService.UserId?.ToString();

        foreach (var entry in ChangeTracker.Entries<BaseAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.Created = now;
                    entry.Entity.CreatedBy = userId;
                    break;

                case EntityState.Modified:
                    entry.Entity.LastModified = now;
                    entry.Entity.LastModifiedBy = userId;
                    break;
            }
        }
    }

    /// <summary>
    /// IUnitOfWork: runs the action inside one database transaction (commit on success, rollback if it
    /// throws). If a transaction is already open the action just joins it. The execution strategy is the
    /// EF-recommended wrapper: if automatic retry-on-failure is ever enabled, the whole block is retried
    /// as a unit instead of leaving a half-finished transaction behind.
    /// </summary>
    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        if (Database.CurrentTransaction is not null)
        {
            return await action();
        }

        var strategy = Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await Database.BeginTransactionAsync(cancellationToken);

            var result = await action();

            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }
}