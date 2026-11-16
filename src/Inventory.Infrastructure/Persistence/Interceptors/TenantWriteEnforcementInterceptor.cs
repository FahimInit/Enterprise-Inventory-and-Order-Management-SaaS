using Inventory.Application.Common;
using Inventory.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Inventory.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Enforces tenant isolation on all database writes (INSERT, UPDATE, DELETE).
/// Prevents cross-tenant modification even if a query bypassed global filters.
/// </summary>
public sealed class TenantWriteEnforcementInterceptor : SaveChangesInterceptor
{
    private readonly ITenantContext _tenantContext;

    public TenantWriteEnforcementInterceptor(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ValidateTenantIntegrity(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ValidateTenantIntegrity(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ValidateTenantIntegrity(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            if (entry.Entity.TenantId == Guid.Empty)
            {
                throw new ForbiddenAccessException(
                    "Tenant.WriteMissingTenantId",
                    $"Cannot persist entity '{entry.Entity.GetType().Name}' with an empty TenantId.");
            }

            if (entry.State == EntityState.Modified &&
                entry.Property(nameof(ITenantEntity.TenantId)).IsModified)
            {
                throw new ForbiddenAccessException(
                    "Tenant.CannotReassignTenantId",
                    $"Changing the TenantId of an existing '{entry.Entity.GetType().Name}' record is forbidden.");
            }

            if (_tenantContext.HasTenant && entry.Entity.TenantId != _tenantContext.RequiredTenantId)
            {
                throw new ForbiddenAccessException(
                    "Tenant.CrossTenantWriteBlocked",
                    $"Cross-tenant write blocked for entity '{entry.Entity.GetType().Name}'. Active tenant does not match entity TenantId.");
            }
        }
    }
}
