namespace Inventory.Application.Identity;

public sealed record RegisterRequest(
    string Email,
    string FullName,
    string Password,
    string? OrganizationName = null,
    string? OrganizationSlug = null,
    string BaseCurrency = "USD",
    string TimeZoneId = "UTC");

public sealed record LoginRequest(
    string Email,
    string Password,
    Guid? PreferredTenantId = null);

public sealed record RefreshTokenRequest(
    string RefreshToken);

public sealed record SwitchOrganizationRequest(
    Guid OrganizationId);

public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);

public sealed record UserWorkspaceDto(
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationSlug,
    string BaseCurrency,
    string RoleName,
    bool IsOwner);

public sealed record AuthResponse(
    Guid UserId,
    string Email,
    string FullName,
    bool IsPlatformAdmin,
    Guid? ActiveTenantId,
    string? ActiveOrganizationName,
    string? ActiveRoleName,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<UserWorkspaceDto> Workspaces,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc);

public sealed record CurrentUserResponse(
    Guid UserId,
    string Email,
    string FullName,
    bool IsPlatformAdmin,
    Guid? ActiveTenantId,
    string? ActiveOrganizationName,
    string? ActiveRoleName,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<UserWorkspaceDto> Workspaces);
