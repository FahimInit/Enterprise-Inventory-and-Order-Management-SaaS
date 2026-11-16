using Inventory.Domain.Auditing;
using Inventory.Domain.Identity;
using Inventory.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Inventory.Application.Common;

public interface IApplicationDbContext
{
    DbSet<ApplicationUser> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Organization> Organizations { get; }
    DbSet<OrganizationRole> OrganizationRoles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<OrganizationMember> OrganizationMembers { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<IdempotencyRecord> IdempotencyRecords { get; }

    DatabaseFacade Database { get; }

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    void RecordAudit(
        string action,
        string entityType,
        string entityId,
        string summary,
        object? beforeState = null,
        object? afterState = null,
        Guid? explicitTenantId = null);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
