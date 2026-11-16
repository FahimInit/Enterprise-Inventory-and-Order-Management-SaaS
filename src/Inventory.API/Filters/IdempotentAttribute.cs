using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Inventory.Application.Common;
using Inventory.Domain.Auditing;
using Inventory.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Inventory.API.Filters;

/// <summary>
/// Enforces server-side idempotency when the client provides an 'Idempotency-Key' header.
/// Prevents duplicate order creations, confirmations, goods receipts, and stock adjustments.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentAttribute : Attribute, IAsyncActionFilter
{
    public const string HeaderName = "Idempotency-Key";
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var keyValues) ||
            string.IsNullOrWhiteSpace(keyValues))
        {
            await next();
            return;
        }

        var idempotencyKey = keyValues.ToString().Trim();
        if (idempotencyKey.Length > 128)
        {
            throw new RequestValidationException(HeaderName, "Idempotency-Key cannot exceed 128 characters.");
        }

        var tenantContext = context.HttpContext.RequestServices.GetRequiredService<ITenantContext>();
        if (!tenantContext.HasTenant)
        {
            await next();
            return;
        }

        var dbContext = context.HttpContext.RequestServices.GetRequiredService<IApplicationDbContext>();
        var tenantId = tenantContext.RequiredTenantId;
        var requestPath = context.HttpContext.Request.Path.ToString();
        var requestMethod = context.HttpContext.Request.Method;

        var serializedArgs = JsonSerializer.Serialize(context.ActionArguments);
        var payloadSignature = $"{requestMethod}:{requestPath}:{serializedArgs}";
        var requestHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payloadSignature)));

        var existingRecord = await dbContext.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.TenantId == tenantId && r.IdempotencyKey == idempotencyKey,
                context.HttpContext.RequestAborted);

        if (existingRecord is not null)
        {
            if (!string.Equals(existingRecord.RequestHash, requestHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new DomainException(
                    "Idempotency.PayloadMismatch",
                    "The provided Idempotency-Key has already been used with a different request payload.");
            }

            if (existingRecord.Status == IdempotencyStatus.InProgress)
            {
                throw new ConflictException(
                    "Idempotency.RequestInProgress",
                    "A request with this Idempotency-Key is currently being processed.");
            }

            if (existingRecord.Status == IdempotencyStatus.Completed && existingRecord.ResponseBodyJson is not null)
            {
                context.HttpContext.Response.Headers["X-Idempotent-Replay"] = "true";
                context.Result = new ContentResult
                {
                    StatusCode = existingRecord.ResponseStatusCode ?? StatusCodes.Status200OK,
                    ContentType = "application/json",
                    Content = existingRecord.ResponseBodyJson
                };
                return;
            }
        }

        var record = new IdempotencyRecord(
            tenantId,
            idempotencyKey,
            requestPath,
            requestMethod,
            requestHash,
            DefaultTtl);

        dbContext.IdempotencyRecords.Add(record);
        await dbContext.SaveChangesAsync(context.HttpContext.RequestAborted);

        var executedContext = await next();

        if (executedContext.Exception is not null)
        {
            // Remove the InProgress idempotency record so failed requests can be safely retried
            var trackedRecord = await dbContext.IdempotencyRecords
                .FirstOrDefaultAsync(r => r.Id == record.Id, context.HttpContext.RequestAborted);

            if (trackedRecord is not null)
            {
                dbContext.IdempotencyRecords.Remove(trackedRecord);
                await dbContext.SaveChangesAsync(context.HttpContext.RequestAborted);
            }

            return;
        }

        if (executedContext.Result is ObjectResult objectResult &&
            (objectResult.StatusCode ?? 200) is >= 200 and < 300)
        {
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var responseJson = JsonSerializer.Serialize(objectResult.Value, jsonOptions);
            var statusCode = objectResult.StatusCode ?? StatusCodes.Status200OK;

            var trackedRecord = await dbContext.IdempotencyRecords
                .FirstOrDefaultAsync(r => r.Id == record.Id, context.HttpContext.RequestAborted);

            if (trackedRecord is not null)
            {
                trackedRecord.MarkCompleted(statusCode, responseJson);
                await dbContext.SaveChangesAsync(context.HttpContext.RequestAborted);
            }
        }
    }
}
