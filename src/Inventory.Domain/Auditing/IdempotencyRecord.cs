using Inventory.Domain.Common;

namespace Inventory.Domain.Auditing;

public enum IdempotencyStatus
{
    InProgress = 1,
    Completed = 2
}

public sealed class IdempotencyRecord : EntityBase, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RequestPath { get; private set; } = string.Empty;
    public string RequestMethod { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public IdempotencyStatus Status { get; private set; }
    public int? ResponseStatusCode { get; private set; }
    public string? ResponseBodyJson { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }

    private IdempotencyRecord()
    {
    }

    public IdempotencyRecord(
        Guid tenantId,
        string idempotencyKey,
        string requestPath,
        string requestMethod,
        string requestHash,
        TimeSpan ttl)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainException("Idempotency.TenantRequired", "TenantId is required for idempotency tracking.");
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new DomainException("Idempotency.KeyRequired", "Idempotency key cannot be empty.");
        }

        TenantId = tenantId;
        IdempotencyKey = idempotencyKey.Trim();
        RequestPath = requestPath.Trim();
        RequestMethod = requestMethod.Trim().ToUpperInvariant();
        RequestHash = requestHash;
        Status = IdempotencyStatus.InProgress;
        ExpiresAtUtc = DateTimeOffset.UtcNow.Add(ttl);
    }

    public void MarkCompleted(int responseStatusCode, string responseBodyJson)
    {
        Status = IdempotencyStatus.Completed;
        ResponseStatusCode = responseStatusCode;
        ResponseBodyJson = responseBodyJson;
        CompletedAtUtc = DateTimeOffset.UtcNow;
        TouchUpdatedTimestamp();
    }
}
