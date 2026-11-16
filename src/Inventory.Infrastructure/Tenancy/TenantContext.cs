using Inventory.Application.Common;
using Inventory.Domain.Common;

namespace Inventory.Infrastructure.Tenancy;

public sealed class TenantContext : ITenantContext
{
    private readonly HashSet<string> _permissions = new(StringComparer.OrdinalIgnoreCase);

    public Guid? TenantId { get; private set; }

    public Guid RequiredTenantId =>
        TenantId is { } id && id != Guid.Empty
            ? id
            : throw new ForbiddenAccessException("Tenant.ContextMissing", "An active organization workspace context is required for this operation.");

    public bool HasTenant => TenantId.HasValue && TenantId.Value != Guid.Empty;

    public Guid? UserId { get; private set; }

    public string? UserEmail { get; private set; }

    public bool IsAuthenticated => UserId.HasValue && UserId.Value != Guid.Empty;

    public bool IsPlatformAdmin { get; private set; }

    public string? CorrelationId { get; private set; }

    public string? IpAddress { get; private set; }

    public IReadOnlySet<string> Permissions => _permissions;

    public bool HasPermission(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            return false;
        }

        return _permissions.Contains(permission);
    }

    public void SetTenantContext(
        Guid tenantId,
        Guid? userId,
        string? userEmail,
        IEnumerable<string> permissions,
        bool isPlatformAdmin = false)
    {
        TenantId = tenantId == Guid.Empty ? null : tenantId;
        UserId = userId;
        UserEmail = userEmail;
        IsPlatformAdmin = isPlatformAdmin;

        _permissions.Clear();
        foreach (var permission in permissions)
        {
            if (!string.IsNullOrWhiteSpace(permission))
            {
                _permissions.Add(permission);
            }
        }
    }

    public void SetUserOnly(Guid userId, string? userEmail, bool isPlatformAdmin = false)
    {
        UserId = userId;
        UserEmail = userEmail;
        IsPlatformAdmin = isPlatformAdmin;
    }

    public void SetRequestMetadata(string? correlationId, string? ipAddress)
    {
        CorrelationId = correlationId;
        IpAddress = ipAddress;
    }
}
