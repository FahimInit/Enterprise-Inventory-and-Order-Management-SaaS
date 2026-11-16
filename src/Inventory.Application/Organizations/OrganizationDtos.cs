namespace Inventory.Application.Organizations;

public sealed record CreateOrganizationRequest(
    string Name,
    string? Slug = null,
    string? Description = null,
    string BaseCurrency = "USD",
    string TimeZoneId = "UTC",
    int DefaultLowStockThreshold = 10,
    decimal DefaultTaxRatePercent = 0m);

public sealed record UpdateOrganizationRequest(
    string Name,
    string Slug,
    string? Description,
    string BaseCurrency,
    string TimeZoneId,
    int DefaultLowStockThreshold,
    decimal DefaultTaxRatePercent);

public sealed record InviteMemberRequest(
    string Email,
    string FullName,
    Guid RoleId,
    string? InitialPassword = null);

public sealed record UpdateMemberRoleRequest(
    Guid RoleId);

public sealed record CreateRoleRequest(
    string Name,
    string? Description,
    IReadOnlyList<string> Permissions);

public sealed record UpdateRoleRequest(
    string Name,
    string? Description,
    IReadOnlyList<string> Permissions);

public sealed record OrganizationDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    string BaseCurrency,
    string TimeZoneId,
    int DefaultLowStockThreshold,
    decimal DefaultTaxRatePercent,
    bool IsActive,
    int ActiveMemberCount,
    DateTimeOffset CreatedAtUtc);

public sealed record OrganizationMemberDto(
    Guid Id,
    Guid UserId,
    string Email,
    string FullName,
    Guid RoleId,
    string RoleName,
    bool IsOwner,
    bool IsActive,
    DateTimeOffset JoinedAtUtc);

public sealed record OrganizationRoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    IReadOnlyList<string> Permissions,
    int AssignedMemberCount);
