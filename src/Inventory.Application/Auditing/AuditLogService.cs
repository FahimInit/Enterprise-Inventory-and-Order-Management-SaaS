using Inventory.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.Auditing;

public sealed record AuditLogFilterRequest : PagedRequest
{
    public string? EntityType { get; init; }
    public string? Action { get; init; }
    public Guid? ActorUserId { get; init; }
}

public sealed record AuditLogDto(
    Guid Id,
    Guid? TenantId,
    Guid? ActorUserId,
    string? ActorEmail,
    string Action,
    string EntityType,
    string EntityId,
    string Summary,
    string? BeforeStateJson,
    string? AfterStateJson,
    string? CorrelationId,
    string? IpAddress,
    DateTimeOffset OccurredAtUtc);

public interface IAuditLogService
{
    Task<PagedResponse<AuditLogDto>> GetAuditLogsAsync(AuditLogFilterRequest request, CancellationToken cancellationToken = default);
}

public sealed class AuditLogService : IAuditLogService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public AuditLogService(IApplicationDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<PagedResponse<AuditLogDto>> GetAuditLogsAsync(
        AuditLogFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var query = _dbContext.AuditLogs
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(request.EntityType))
        {
            var entityType = request.EntityType.Trim();
            query = query.Where(a => a.EntityType == entityType);
        }

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            var action = request.Action.Trim();
            query = query.Where(a => a.Action == action);
        }

        if (request.ActorUserId.HasValue)
        {
            query = query.Where(a => a.ActorUserId == request.ActorUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(a =>
                a.Summary.ToLower().Contains(term) ||
                a.Action.ToLower().Contains(term) ||
                a.EntityType.ToLower().Contains(term) ||
                (a.ActorEmail != null && a.ActorEmail.ToLower().Contains(term)));
        }

        query = query.OrderByDescending(a => a.OccurredAtUtc);

        var projected = query.Select(a => new AuditLogDto(
            a.Id,
            a.TenantId,
            a.ActorUserId,
            a.ActorEmail,
            a.Action,
            a.EntityType,
            a.EntityId,
            a.Summary,
            a.BeforeStateJson,
            a.AfterStateJson,
            a.CorrelationId,
            a.IpAddress,
            a.OccurredAtUtc));

        return await PagedResponse<AuditLogDto>.CreateAsync(
            projected,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
    }
}
