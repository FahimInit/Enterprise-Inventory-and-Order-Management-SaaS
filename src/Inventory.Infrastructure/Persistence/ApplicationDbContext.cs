using System.Linq.Expressions;
using System.Text.Json;
using Inventory.Application.Common;
using Inventory.Domain.Auditing;
using Inventory.Domain.Catalog;
using Inventory.Domain.Common;
using Inventory.Domain.Identity;
using Inventory.Domain.Organizations;
using Inventory.Domain.Suppliers;
using Inventory.Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Inventory.Infrastructure.Persistence;

public sealed class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly ITenantContext _tenantContext;

    public Guid? CurrentTenantId => _tenantContext.TenantId;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationRole> OrganizationRoles => Set<OrganizationRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierProduct> SupplierProducts => Set<SupplierProduct>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<WarehouseLocation> WarehouseLocations => Set<WarehouseLocation>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (Database.CurrentTransaction is not null)
        {
            return Database.CurrentTransaction;
        }

        return await Database.BeginTransactionAsync(cancellationToken);
    }

    public void RecordAudit(
        string action,
        string entityType,
        string entityId,
        string summary,
        object? beforeState = null,
        object? afterState = null,
        Guid? explicitTenantId = null)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var auditLog = new AuditLog(
            tenantId: explicitTenantId ?? _tenantContext.TenantId,
            actorUserId: _tenantContext.UserId,
            actorEmail: _tenantContext.UserEmail,
            action: action,
            entityType: entityType,
            entityId: entityId,
            summary: summary,
            beforeStateJson: beforeState is null ? null : JsonSerializer.Serialize(beforeState, jsonOptions),
            afterStateJson: afterState is null ? null : JsonSerializer.Serialize(afterState, jsonOptions),
            correlationId: _tenantContext.CorrelationId,
            ipAddress: _tenantContext.IpAddress);

        AuditLogs.Add(auditLog);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(CreateTenantFilterExpression(entityType.ClrType));
            }
        }
    }

    private LambdaExpression CreateTenantFilterExpression(Type entityType)
    {
        var parameter = Expression.Parameter(entityType, "e");
        var tenantProperty = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
        var currentTenantProperty = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));
        var nullableTenantProperty = Expression.Convert(tenantProperty, typeof(Guid?));
        var equality = Expression.Equal(nullableTenantProperty, currentTenantProperty);

        return Expression.Lambda(equality, parameter);
    }
}
