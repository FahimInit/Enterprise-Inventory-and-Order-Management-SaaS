using System.Text.Json;
using Inventory.Application.Common;
using Inventory.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventory.API.Middleware;

public sealed class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, tenantContext, exception);
        }
    }

    private async Task HandleExceptionAsync(
        HttpContext context,
        ITenantContext tenantContext,
        Exception exception)
    {
        var (statusCode, title, errorCode, detail, validationErrors) = MapException(exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Unhandled exception occurred for request {Method} {Path} [CorrelationId: {CorrelationId}, TenantId: {TenantId}]",
                context.Request.Method,
                context.Request.Path,
                tenantContext.CorrelationId ?? context.TraceIdentifier,
                tenantContext.TenantId);
        }
        else
        {
            _logger.LogWarning(
                "Domain/Application error {ErrorCode} ({StatusCode}) on {Method} {Path}: {Message}",
                errorCode,
                statusCode,
                context.Request.Method,
                context.Request.Path,
                exception.Message);
        }

        if (context.Response.HasStarted)
        {
            return;
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path,
            Type = GetRfcTypeUri(statusCode)
        };

        problemDetails.Extensions["errorCode"] = errorCode;
        problemDetails.Extensions["correlationId"] = tenantContext.CorrelationId ?? context.TraceIdentifier;
        problemDetails.Extensions["timestampUtc"] = DateTimeOffset.UtcNow;

        if (validationErrors is not null)
        {
            problemDetails.Extensions["errors"] = validationErrors;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, jsonOptions));
    }

    private (int StatusCode, string Title, string ErrorCode, string Detail, IReadOnlyDictionary<string, string[]>? ValidationErrors) MapException(Exception exception)
    {
        return exception switch
        {
            RequestValidationException validationEx => (
                StatusCodes.Status400BadRequest,
                "Validation Failed",
                validationEx.ErrorCode,
                validationEx.Message,
                validationEx.Errors),

            NotFoundException notFoundEx => (
                StatusCodes.Status404NotFound,
                "Resource Not Found",
                notFoundEx.ErrorCode,
                notFoundEx.Message,
                null),

            ForbiddenAccessException forbiddenEx => (
                StatusCodes.Status403Forbidden,
                "Access Denied",
                forbiddenEx.ErrorCode,
                forbiddenEx.Message,
                null),

            ConflictException conflictEx => (
                StatusCodes.Status409Conflict,
                "Business Rule Conflict",
                conflictEx.ErrorCode,
                conflictEx.Message,
                null),

            DomainException domainEx => (
                StatusCodes.Status422UnprocessableEntity,
                "Business Rule Violation",
                domainEx.ErrorCode,
                domainEx.Message,
                null),

            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "Concurrency Conflict",
                "Database.ConcurrencyConflict",
                "The record was modified by another user or process. Please refresh and retry the operation.",
                null),

            UnauthorizedAccessException unauthorizedEx => (
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "Security.Unauthorized",
                string.IsNullOrWhiteSpace(unauthorizedEx.Message)
                    ? "Authentication is required to access this resource."
                    : unauthorizedEx.Message,
                null),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "Server.UnexpectedError",
                _environment.IsDevelopment()
                    ? exception.Message
                    : "An unexpected error occurred while processing your request.",
                null)
        };
    }

    private static string GetRfcTypeUri(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        StatusCodes.Status401Unauthorized => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
        StatusCodes.Status403Forbidden => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
        StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
        StatusCodes.Status422UnprocessableEntity => "https://tools.ietf.org/html/rfc4918#section-11.2",
        _ => "https://tools.ietf.org/html/rfc9110#section-15.6.1"
    };
}
