namespace Inventory.Application.Common;

public interface ITenantContext
{
    Guid? TenantId { get; }
    Guid RequiredTenantId { get; }
    bool HasTenant { get; }
    Guid? UserId { get; }
    string? UserEmail { get; }
    bool IsAuthenticated { get; }
    bool IsPlatformAdmin { get; }
    string? CorrelationId { get; }
    string? IpAddress { get; }
    IReadOnlySet<string> Permissions { get; }

    bool HasPermission(string permission);
    void SetTenantContext(Guid tenantId, Guid? userId, string? userEmail, IEnumerable<string> permissions, bool isPlatformAdmin = false);
    void SetRequestMetadata(string? correlationId, string? ipAddress);
}
