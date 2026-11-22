using Inventory.Application.Common;
using Inventory.Domain.Common;
using Inventory.Domain.Identity;
using Inventory.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.Identity;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task LogoutAsync(string? rawRefreshToken, CancellationToken cancellationToken = default);
    Task<AuthResponse> SwitchOrganizationAsync(SwitchOrganizationRequest request, CancellationToken cancellationToken = default);
    Task<CurrentUserResponse> GetCurrentUserAsync(CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default);
}

public sealed class AuthService : IAuthService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IPasswordHashingService _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly ITenantContext _tenantContext;
    private readonly RegisterRequestValidator _registerValidator = new();
    private readonly LoginRequestValidator _loginValidator = new();
    private readonly ChangePasswordRequestValidator _changePasswordValidator = new();

    public AuthService(
        IApplicationDbContext dbContext,
        IPasswordHashingService passwordHasher,
        ITokenService tokenService,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _tenantContext = tenantContext;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        await _registerValidator.ValidateAndThrowProblemAsync(request, cancellationToken);

        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var emailExists = await _dbContext.Users
            .AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (emailExists)
        {
            throw new ConflictException("User.EmailAlreadyRegistered", $"An account with email '{request.Email.Trim()}' already exists.");
        }

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);

        var user = new ApplicationUser(request.Email, request.FullName, "PENDING_HASH");
        var passwordHash = _passwordHasher.HashPassword(user, request.Password);
        user.UpdatePasswordHash(passwordHash);

        _dbContext.Users.Add(user);

        Guid? activeTenantId = null;
        if (!string.IsNullOrWhiteSpace(request.OrganizationName))
        {
            var requestedSlug = string.IsNullOrWhiteSpace(request.OrganizationSlug)
                ? Organization.NormalizeSlug(request.OrganizationName)
                : Organization.NormalizeSlug(request.OrganizationSlug);

            var slugExists = await _dbContext.Organizations
                .AnyAsync(o => o.Slug == requestedSlug, cancellationToken);

            if (slugExists)
            {
                requestedSlug = $"{requestedSlug}-{Guid.NewGuid().ToString("N")[..6]}";
            }

            var organization = new Organization(
                request.OrganizationName,
                requestedSlug,
                description: null,
                baseCurrency: request.BaseCurrency,
                timeZoneId: request.TimeZoneId);

            _dbContext.Organizations.Add(organization);
            activeTenantId = organization.Id;

            var ownerRole = SeedDefaultRolesForOrganization(organization.Id);
            var membership = new OrganizationMember(
                organization.Id,
                user.Id,
                ownerRole.Id,
                isOwner: true,
                invitedByUserId: user.Id);

            _dbContext.OrganizationMembers.Add(membership);

            _dbContext.RecordAudit(
                action: "Organization.Created",
                entityType: nameof(Organization),
                entityId: organization.Id.ToString(),
                summary: $"Created organization '{organization.Name}' ({organization.Slug}) during registration.",
                afterState: new { organization.Id, organization.Name, organization.Slug, OwnerUserId = user.Id },
                explicitTenantId: organization.Id);
        }

        user.RecordLoginSuccess(DateTimeOffset.UtcNow);

        _dbContext.RecordAudit(
            action: "User.Registered",
            entityType: nameof(ApplicationUser),
            entityId: user.Id.ToString(),
            summary: $"User '{user.Email}' registered.",
            afterState: new { user.Id, user.Email, user.FullName },
            explicitTenantId: activeTenantId);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var authResponse = await IssueSessionTokensAsync(
            user,
            activeTenantId,
            familyId: Guid.CreateVersion7(),
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return authResponse;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        await _loginValidator.ValidateAndThrowProblemAsync(request, cancellationToken);

        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        var now = DateTimeOffset.UtcNow;
        user.EnsureCanAuthenticate(now);

        if (!_passwordHasher.VerifyPassword(user, user.PasswordHash, request.Password))
        {
            user.RecordLoginFailure(now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        user.RecordLoginSuccess(now);

        var authResponse = await IssueSessionTokensAsync(
            user,
            request.PreferredTenantId,
            familyId: Guid.CreateVersion7(),
            cancellationToken);

        _dbContext.RecordAudit(
            action: "User.LoggedIn",
            entityType: nameof(ApplicationUser),
            entityId: user.Id.ToString(),
            summary: $"User '{user.Email}' logged in.",
            explicitTenantId: authResponse.ActiveTenantId);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return authResponse;
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            throw new RequestValidationException(nameof(request.RefreshToken), "Refresh token is required.");
        }

        var tokenHash = _tokenService.HashRefreshToken(request.RefreshToken);
        var existingToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (existingToken is null)
        {
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        var now = DateTimeOffset.UtcNow;

        if (existingToken.IsUsed || existingToken.IsRevoked)
        {
            // Token reuse detected: revoke all tokens in the same family immediately.
            var familyTokens = await _dbContext.RefreshTokens
                .Where(t => t.FamilyId == existingToken.FamilyId && !t.IsRevoked)
                .ToListAsync(cancellationToken);

            foreach (var familyToken in familyTokens)
            {
                familyToken.Revoke("Compromised token family: refresh token reuse detected.");
            }

            _dbContext.RecordAudit(
                action: "Auth.RefreshTokenReuseDetected",
                entityType: nameof(RefreshToken),
                entityId: existingToken.Id.ToString(),
                summary: $"Refresh token reuse detected for FamilyId '{existingToken.FamilyId}'. Revoked {familyTokens.Count} family tokens.",
                explicitTenantId: existingToken.ActiveTenantId);

            await _dbContext.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("Refresh token has already been used or revoked. Session terminated for security.");
        }

        if (existingToken.IsExpired(now))
        {
            existingToken.Revoke("Token expired.");
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("Refresh token has expired.");
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == existingToken.UserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("User account not found.");

        user.EnsureCanAuthenticate(now);

        var authResponse = await IssueSessionTokensAsync(
            user,
            existingToken.ActiveTenantId,
            existingToken.FamilyId,
            cancellationToken,
            tokenBeingRotated: existingToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return authResponse;
    }

    public async Task LogoutAsync(string? rawRefreshToken, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            var tokenHash = _tokenService.HashRefreshToken(rawRefreshToken);
            var token = await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

            if (token is not null)
            {
                var familyTokens = await _dbContext.RefreshTokens
                    .Where(t => t.FamilyId == token.FamilyId && !t.IsRevoked)
                    .ToListAsync(cancellationToken);

                foreach (var item in familyTokens)
                {
                    item.Revoke("User logged out.");
                }
            }
        }
        else if (_tenantContext.UserId.HasValue)
        {
            var activeTokens = await _dbContext.RefreshTokens
                .Where(t => t.UserId == _tenantContext.UserId.Value && !t.IsRevoked && !t.IsUsed)
                .ToListAsync(cancellationToken);

            foreach (var item in activeTokens)
            {
                item.Revoke("User logged out.");
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthResponse> SwitchOrganizationAsync(
        SwitchOrganizationRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _tenantContext.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("User not found.");

        user.EnsureCanAuthenticate(DateTimeOffset.UtcNow);

        var hasActiveMembership = await _dbContext.OrganizationMembers
            .IgnoreQueryFilters()
            .AnyAsync(
                m => m.UserId == userId &&
                     m.TenantId == request.OrganizationId &&
                     m.IsActive,
                cancellationToken);

        if (!hasActiveMembership)
        {
            throw new ForbiddenAccessException("Organization.NotAMember", "You do not have an active membership in the requested organization.");
        }

        var response = await IssueSessionTokensAsync(
            user,
            request.OrganizationId,
            familyId: Guid.CreateVersion7(),
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task<CurrentUserResponse> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var userId = _tenantContext.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(ApplicationUser), userId);

        var (workspaces, activeWorkspace, permissions) = await LoadUserWorkspaceStateAsync(
            user.Id,
            _tenantContext.TenantId,
            cancellationToken);

        return new CurrentUserResponse(
            user.Id,
            user.Email,
            user.FullName,
            user.IsPlatformAdmin,
            activeWorkspace?.OrganizationId,
            activeWorkspace?.OrganizationName,
            activeWorkspace?.RoleName,
            permissions,
            workspaces);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        await _changePasswordValidator.ValidateAndThrowProblemAsync(request, cancellationToken);

        var userId = _tenantContext.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(ApplicationUser), userId);

        if (!_passwordHasher.VerifyPassword(user, user.PasswordHash, request.CurrentPassword))
        {
            throw new DomainException("User.InvalidCurrentPassword", "The current password provided is incorrect.");
        }

        var newHash = _passwordHasher.HashPassword(user, request.NewPassword);
        user.UpdatePasswordHash(newHash);

        _dbContext.RecordAudit(
            action: "User.PasswordChanged",
            entityType: nameof(ApplicationUser),
            entityId: user.Id.ToString(),
            summary: $"User '{user.Email}' changed their password.",
            explicitTenantId: _tenantContext.TenantId);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    internal OrganizationRole SeedDefaultRolesForOrganization(Guid organizationId)
    {
        OrganizationRole? ownerRole = null;
        var definitions = Permissions.GetDefaultRoleDefinitions();

        foreach (var (roleName, (description, rolePermissions)) in definitions)
        {
            var role = new OrganizationRole(
                organizationId,
                roleName,
                description,
                isSystemRole: true,
                rolePermissions);

            _dbContext.OrganizationRoles.Add(role);

            if (roleName == Permissions.SystemRoleNames.Owner)
            {
                ownerRole = role;
            }
        }

        return ownerRole!;
    }

    private async Task<AuthResponse> IssueSessionTokensAsync(
        ApplicationUser user,
        Guid? preferredTenantId,
        Guid familyId,
        CancellationToken cancellationToken,
        RefreshToken? tokenBeingRotated = null)
    {
        var (workspaces, activeWorkspace, permissions) = await LoadUserWorkspaceStateAsync(
            user.Id,
            preferredTenantId,
            cancellationToken);

        var generated = _tokenService.GenerateTokens(
            user,
            activeWorkspace?.OrganizationId,
            activeWorkspace?.RoleName,
            permissions);

        tokenBeingRotated?.MarkAsRotated(generated.RefreshTokenHash);

        var refreshTokenEntity = new RefreshToken(
            user.Id,
            activeWorkspace?.OrganizationId,
            generated.RefreshTokenHash,
            familyId,
            generated.RefreshTokenExpiresAtUtc,
            _tenantContext.IpAddress);

        _dbContext.RefreshTokens.Add(refreshTokenEntity);

        return new AuthResponse(
            user.Id,
            user.Email,
            user.FullName,
            user.IsPlatformAdmin,
            activeWorkspace?.OrganizationId,
            activeWorkspace?.OrganizationName,
            activeWorkspace?.RoleName,
            permissions,
            workspaces,
            generated.AccessToken,
            generated.AccessTokenExpiresAtUtc,
            generated.RawRefreshToken,
            generated.RefreshTokenExpiresAtUtc);
    }

    private async Task<(IReadOnlyList<UserWorkspaceDto> Workspaces, UserWorkspaceDto? ActiveWorkspace, IReadOnlyList<string> Permissions)> LoadUserWorkspaceStateAsync(
        Guid userId,
        Guid? preferredTenantId,
        CancellationToken cancellationToken)
    {
        var memberships = await (
            from member in _dbContext.OrganizationMembers.IgnoreQueryFilters().AsNoTracking()
            join org in _dbContext.Organizations.AsNoTracking() on member.TenantId equals org.Id
            join role in _dbContext.OrganizationRoles.IgnoreQueryFilters().AsNoTracking() on member.RoleId equals role.Id
            where member.UserId == userId && member.IsActive && org.IsActive
            orderby member.JoinedAtUtc
            select new
            {
                member.TenantId,
                OrganizationName = org.Name,
                OrganizationSlug = org.Slug,
                org.BaseCurrency,
                member.RoleId,
                RoleName = role.Name,
                member.IsOwner,
                Permissions = _dbContext.RolePermissions
                    .IgnoreQueryFilters()
                    .Where(rp => rp.TenantId == member.TenantId && rp.RoleId == member.RoleId)
                    .Select(rp => rp.Permission)
                    .OrderBy(p => p)
                    .ToList()
            }).ToListAsync(cancellationToken);

        var workspaces = memberships
            .Select(m => new UserWorkspaceDto(
                m.TenantId,
                m.OrganizationName,
                m.OrganizationSlug,
                m.BaseCurrency,
                m.RoleName,
                m.IsOwner))
            .ToList();

        var activeMembership = preferredTenantId.HasValue
            ? memberships.FirstOrDefault(m => m.TenantId == preferredTenantId.Value) ?? memberships.FirstOrDefault()
            : memberships.FirstOrDefault();

        if (activeMembership is null)
        {
            return (workspaces, null, []);
        }

        var activeWorkspace = workspaces.First(w => w.OrganizationId == activeMembership.TenantId);
        return (workspaces, activeWorkspace, activeMembership.Permissions);
    }
}
