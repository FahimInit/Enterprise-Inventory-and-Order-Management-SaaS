using Inventory.Domain.Common;

namespace Inventory.Domain.Auditing;

public sealed class AuditLog : EntityBase
{
    public Guid? TenantId { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string? ActorEmail { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public string? BeforeStateJson { get; private set; }
    public string? AfterStateJson { get; private set; }
    public string? CorrelationId { get; private set; }
    public string? IpAddress { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    private AuditLog()
    {
    }

    public AuditLog(
        Guid? tenantId,
        Guid? actorUserId,
        string? actorEmail,
        string action,
        string entityType,
        string entityId,
        string summary,
        string? beforeStateJson = null,
        string? afterStateJson = null,
        string? correlationId = null,
        string? ipAddress = null)
    {
        TenantId = tenantId;
        ActorUserId = actorUserId;
        ActorEmail = actorEmail;
        Action = action.Trim();
        EntityType = entityType.Trim();
        EntityId = entityId.Trim();
        Summary = summary.Trim();
        BeforeStateJson = beforeStateJson;
        AfterStateJson = afterStateJson;
        CorrelationId = correlationId;
        IpAddress = ipAddress;
        OccurredAtUtc = DateTimeOffset.UtcNow;
    }
}
