using Inventory.Application.Common;
using Inventory.Application.Identity;
using Inventory.Domain.Common;
using Inventory.Domain.Identity;
using Inventory.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.Organizations;

public interface IOrganizationService
{
    Task<OrganizationDto> CreateOrganizationAsync(CreateOrganizationRequest request, CancellationToken cancellationToken = default);
    Task<OrganizationDto> GetCurrentOrganizationAsync(CancellationToken cancellationToken = default);
    Task<OrganizationDto> UpdateCurrentOrganizationAsync(UpdateOrganizationRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrganizationMemberDto>> GetMembersAsync(CancellationToken cancellationToken = default);
    Task<OrganizationMemberDto> InviteOrAddMemberAsync(InviteMemberRequest request, CancellationToken cancellationToken = default);
    Task<OrganizationMemberDto> UpdateMemberRoleAsync(Guid memberId, UpdateMemberRoleRequest request, CancellationToken cancellationToken = default);
    Task DeactivateMemberAsync(Guid memberId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrganizationRoleDto>> GetRolesAsync(CancellationToken cancellationToken = default);
    Task<OrganizationRoleDto> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);
    Task<OrganizationRoleDto> UpdateRoleAsync(Guid roleId, UpdateRoleRequest request, CancellationToken cancellationToken = default);
    IReadOnlyList<string> GetAvailablePermissions();
}

public sealed class OrganizationService : IOrganizationService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IPasswordHashingService _passwordHasher;
    private readonly CreateOrganizationRequestValidator _createOrgValidator = new();
    private readonly UpdateOrganizationRequestValidator _updateOrgValidator = new();
    private readonly InviteMemberRequestValidator _inviteValidator = new();
    private readonly CreateRoleRequestValidator _createRoleValidator = new();

    public OrganizationService(
        IApplicationDbContext dbContext,
        ITenantContext tenantContext,
        IPasswordHashingService passwordHasher)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _passwordHasher = passwordHasher;
    }

    public async Task<OrganizationDto> CreateOrganizationAsync(
        CreateOrganizationRequest request,
        CancellationToken cancellationToken = default)
    {
        await _createOrgValidator.ValidateAndThrowProblemAsync(request, cancellationToken);

        var userId = _tenantContext.UserId
            ?? throw new UnauthorizedAccessException("Authentication is required to create an organization.");

        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? Organization.NormalizeSlug(request.Name)
            : Organization.NormalizeSlug(request.Slug);

        var slugExists = await _dbContext.Organizations
            .AnyAsync(o => o.Slug == slug, cancellationToken);

        if (slugExists)
        {
            throw new ConflictException("Organization.SlugAlreadyExists", $"Organization slug '{slug}' is already in use.");
        }

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);

        var organization = new Organization(
            request.Name,
            slug,
            request.Description,
            request.BaseCurrency,
            request.TimeZoneId,
            request.DefaultLowStockThreshold,
            request.DefaultTaxRatePercent);

        _dbContext.Organizations.Add(organization);

        OrganizationRole? ownerRole = null;
        foreach (var (roleName, (description, rolePermissions)) in Permissions.GetDefaultRoleDefinitions())
        {
            var role = new OrganizationRole(
                organization.Id,
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

        var membership = new OrganizationMember(
            organization.Id,
            userId,
            ownerRole!.Id,
            isOwner: true,
            invitedByUserId: userId);

        _dbContext.OrganizationMembers.Add(membership);

        _dbContext.RecordAudit(
            action: "Organization.Created",
            entityType: nameof(Organization),
            entityId: organization.Id.ToString(),
            summary: $"Created organization '{organization.Name}' ({organization.Slug}).",
            afterState: new { organization.Id, organization.Name, organization.Slug },
            explicitTenantId: organization.Id);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OrganizationDto(
            organization.Id,
            organization.Name,
            organization.Slug,
            organization.Description,
            organization.BaseCurrency,
            organization.TimeZoneId,
            organization.DefaultLowStockThreshold,
            organization.DefaultTaxRatePercent,
            organization.IsActive,
            ActiveMemberCount: 1,
            organization.CreatedAtUtc);
    }

    public async Task<OrganizationDto> GetCurrentOrganizationAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var organization = await _dbContext.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Organization), tenantId);

        var activeMembers = await _dbContext.OrganizationMembers
            .CountAsync(m => m.TenantId == tenantId && m.IsActive, cancellationToken);

        return new OrganizationDto(
            organization.Id,
            organization.Name,
            organization.Slug,
            organization.Description,
            organization.BaseCurrency,
            organization.TimeZoneId,
            organization.DefaultLowStockThreshold,
            organization.DefaultTaxRatePercent,
            organization.IsActive,
            activeMembers,
            organization.CreatedAtUtc);
    }

    public async Task<OrganizationDto> UpdateCurrentOrganizationAsync(
        UpdateOrganizationRequest request,
        CancellationToken cancellationToken = default)
    {
        await _updateOrgValidator.ValidateAndThrowProblemAsync(request, cancellationToken);

        var tenantId = _tenantContext.RequiredTenantId;

        var organization = await _dbContext.Organizations
            .FirstOrDefaultAsync(o => o.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Organization), tenantId);

        var normalizedSlug = Organization.NormalizeSlug(request.Slug);
        var slugConflict = await _dbContext.Organizations
            .AnyAsync(o => o.Id != tenantId && o.Slug == normalizedSlug, cancellationToken);

        if (slugConflict)
        {
            throw new ConflictException("Organization.SlugAlreadyExists", $"Slug '{normalizedSlug}' is already taken by another organization.");
        }

        var before = new
        {
            organization.Name,
            organization.Slug,
            organization.BaseCurrency,
            organization.TimeZoneId,
            organization.DefaultLowStockThreshold,
            organization.DefaultTaxRatePercent
        };

        organization.UpdateSettings(
            request.Name,
            normalizedSlug,
            request.Description,
            request.BaseCurrency,
            request.TimeZoneId,
            request.DefaultLowStockThreshold,
            request.DefaultTaxRatePercent);

        _dbContext.RecordAudit(
            action: "Organization.Updated",
            entityType: nameof(Organization),
            entityId: organization.Id.ToString(),
            summary: $"Updated workspace settings for '{organization.Name}'.",
            beforeState: before,
            afterState: new
            {
                organization.Name,
                organization.Slug,
                organization.BaseCurrency,
                organization.TimeZoneId,
                organization.DefaultLowStockThreshold,
                organization.DefaultTaxRatePercent
            });

        await _dbContext.SaveChangesAsync(cancellationToken);

        var activeMembers = await _dbContext.OrganizationMembers
            .CountAsync(m => m.TenantId == tenantId && m.IsActive, cancellationToken);

        return new OrganizationDto(
            organization.Id,
            organization.Name,
            organization.Slug,
            organization.Description,
            organization.BaseCurrency,
            organization.TimeZoneId,
            organization.DefaultLowStockThreshold,
            organization.DefaultTaxRatePercent,
            organization.IsActive,
            activeMembers,
            organization.CreatedAtUtc);
    }

    public async Task<IReadOnlyList<OrganizationMemberDto>> GetMembersAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var members = await (
            from m in _dbContext.OrganizationMembers.AsNoTracking()
            join u in _dbContext.Users.AsNoTracking() on m.UserId equals u.Id
            join r in _dbContext.OrganizationRoles.AsNoTracking() on m.RoleId equals r.Id
            where m.TenantId == tenantId
            orderby m.IsOwner descending, u.FullName
            select new OrganizationMemberDto(
                m.Id,
                m.UserId,
                u.Email,
                u.FullName,
                m.RoleId,
                r.Name,
                m.IsOwner,
                m.IsActive,
                m.JoinedAtUtc)).ToListAsync(cancellationToken);

        return members;
    }

    public async Task<OrganizationMemberDto> InviteOrAddMemberAsync(
        InviteMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        await _inviteValidator.ValidateAndThrowProblemAsync(request, cancellationToken);

        var tenantId = _tenantContext.RequiredTenantId;

        var role = await _dbContext.OrganizationRoles
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == request.RoleId, cancellationToken)
            ?? throw new NotFoundException(nameof(OrganizationRole), request.RoleId);

        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
        {
            var initialPassword = string.IsNullOrWhiteSpace(request.InitialPassword)
                ? $"TempPass!{Guid.NewGuid():N}"[..16] + "1Aa"
                : request.InitialPassword;

            user = new ApplicationUser(request.Email, request.FullName, "PENDING");
            user.UpdatePasswordHash(_passwordHasher.HashPassword(user, initialPassword));
            _dbContext.Users.Add(user);
        }

        var existingMembership = await _dbContext.OrganizationMembers
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.UserId == user.Id, cancellationToken);

        if (existingMembership is not null)
        {
            if (existingMembership.IsActive)
            {
                throw new ConflictException("Member.AlreadyExists", $"User '{user.Email}' is already an active member of this organization.");
            }

            existingMembership.Activate();
            existingMembership.ChangeRole(
                role.Id,
                string.Equals(role.Name, Permissions.SystemRoleNames.Owner, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            existingMembership = new OrganizationMember(
                tenantId,
                user.Id,
                role.Id,
                isOwner: string.Equals(role.Name, Permissions.SystemRoleNames.Owner, StringComparison.OrdinalIgnoreCase),
                invitedByUserId: _tenantContext.UserId);

            _dbContext.OrganizationMembers.Add(existingMembership);
        }

        _dbContext.RecordAudit(
            action: "OrganizationMember.Added",
            entityType: nameof(OrganizationMember),
            entityId: existingMembership.Id.ToString(),
            summary: $"Added member '{user.Email}' with role '{role.Name}'.",
            afterState: new { existingMembership.Id, user.Email, RoleName = role.Name });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new OrganizationMemberDto(
            existingMembership.Id,
            user.Id,
            user.Email,
            user.FullName,
            role.Id,
            role.Name,
            existingMembership.IsOwner,
            existingMembership.IsActive,
            existingMembership.JoinedAtUtc);
    }

    public async Task<OrganizationMemberDto> UpdateMemberRoleAsync(
        Guid memberId,
        UpdateMemberRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var member = await _dbContext.OrganizationMembers
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == memberId, cancellationToken)
            ?? throw new NotFoundException(nameof(OrganizationMember), memberId);

        var role = await _dbContext.OrganizationRoles
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == request.RoleId, cancellationToken)
            ?? throw new NotFoundException(nameof(OrganizationRole), request.RoleId);

        var isTargetRoleOwner = string.Equals(role.Name, Permissions.SystemRoleNames.Owner, StringComparison.OrdinalIgnoreCase);

        if (member.IsOwner && !isTargetRoleOwner)
        {
            var otherOwnerCount = await _dbContext.OrganizationMembers
                .CountAsync(m => m.TenantId == tenantId && m.IsActive && m.IsOwner && m.Id != member.Id, cancellationToken);

            if (otherOwnerCount == 0)
            {
                throw new DomainException("Member.LastOwnerProtection", "Cannot remove Owner role from the only active Owner of the organization.");
            }
        }

        var oldRoleId = member.RoleId;
        member.ChangeRole(role.Id, isTargetRoleOwner);

        _dbContext.RecordAudit(
            action: "OrganizationMember.RoleUpdated",
            entityType: nameof(OrganizationMember),
            entityId: member.Id.ToString(),
            summary: $"Updated role for member '{member.Id}' to '{role.Name}'.",
            beforeState: new { RoleId = oldRoleId },
            afterState: new { RoleId = role.Id, RoleName = role.Name });

        await _dbContext.SaveChangesAsync(cancellationToken);

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstAsync(u => u.Id == member.UserId, cancellationToken);

        return new OrganizationMemberDto(
            member.Id,
            member.UserId,
            user.Email,
            user.FullName,
            role.Id,
            role.Name,
            member.IsOwner,
            member.IsActive,
            member.JoinedAtUtc);
    }

    public async Task DeactivateMemberAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var member = await _dbContext.OrganizationMembers
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == memberId, cancellationToken)
            ?? throw new NotFoundException(nameof(OrganizationMember), memberId);

        member.Deactivate();

        // Revoke active refresh tokens scoped to this tenant for the deactivated user
        var activeTokens = await _dbContext.RefreshTokens
            .Where(rt => rt.UserId == member.UserId && rt.ActiveTenantId == tenantId && !rt.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.Revoke("Member deactivated from organization.");
        }

        _dbContext.RecordAudit(
            action: "OrganizationMember.Deactivated",
            entityType: nameof(OrganizationMember),
            entityId: member.Id.ToString(),
            summary: $"Deactivated member '{member.Id}' (UserId: {member.UserId}).");

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrganizationRoleDto>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var roles = await _dbContext.OrganizationRoles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.IsSystemRole)
            .ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);

        var memberCounts = await _dbContext.OrganizationMembers
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.IsActive)
            .GroupBy(m => m.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count, cancellationToken);

        return roles
            .Select(r => new OrganizationRoleDto(
                r.Id,
                r.Name,
                r.Description,
                r.IsSystemRole,
                r.Permissions.Select(p => p.Permission).OrderBy(p => p).ToList(),
                memberCounts.GetValueOrDefault(r.Id, 0)))
            .ToList();
    }

    public async Task<OrganizationRoleDto> CreateRoleAsync(
        CreateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        await _createRoleValidator.ValidateAndThrowProblemAsync(request, cancellationToken);

        var tenantId = _tenantContext.RequiredTenantId;
        var trimmedName = request.Name.Trim();

        var exists = await _dbContext.OrganizationRoles
            .AnyAsync(r => r.TenantId == tenantId && r.Name.ToLower() == trimmedName.ToLower(), cancellationToken);

        if (exists)
        {
            throw new ConflictException("Role.NameAlreadyExists", $"A role named '{trimmedName}' already exists in this organization.");
        }

        var role = new OrganizationRole(
            tenantId,
            trimmedName,
            request.Description,
            isSystemRole: false,
            request.Permissions);

        _dbContext.OrganizationRoles.Add(role);

        _dbContext.RecordAudit(
            action: "OrganizationRole.Created",
            entityType: nameof(OrganizationRole),
            entityId: role.Id.ToString(),
            summary: $"Created custom role '{role.Name}' with {role.Permissions.Count} permissions.",
            afterState: new { role.Id, role.Name, Permissions = role.Permissions.Select(p => p.Permission) });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new OrganizationRoleDto(
            role.Id,
            role.Name,
            role.Description,
            role.IsSystemRole,
            role.Permissions.Select(p => p.Permission).OrderBy(p => p).ToList(),
            AssignedMemberCount: 0);
    }

    public async Task<OrganizationRoleDto> UpdateRoleAsync(
        Guid roleId,
        UpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var role = await _dbContext.OrganizationRoles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == roleId, cancellationToken)
            ?? throw new NotFoundException(nameof(OrganizationRole), roleId);

        if (string.Equals(role.Name, Permissions.SystemRoleNames.Owner, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("Role.CannotModifyOwnerPermissions", "The Owner role must retain all workspace permissions.");
        }

        role.UpdateRole(request.Name, request.Description, request.Permissions);

        _dbContext.RecordAudit(
            action: "OrganizationRole.Updated",
            entityType: nameof(OrganizationRole),
            entityId: role.Id.ToString(),
            summary: $"Updated role '{role.Name}'.",
            afterState: new { role.Id, role.Name, Permissions = role.Permissions.Select(p => p.Permission) });

        await _dbContext.SaveChangesAsync(cancellationToken);

        var assignedCount = await _dbContext.OrganizationMembers
            .CountAsync(m => m.TenantId == tenantId && m.RoleId == role.Id && m.IsActive, cancellationToken);

        return new OrganizationRoleDto(
            role.Id,
            role.Name,
            role.Description,
            role.IsSystemRole,
            role.Permissions.Select(p => p.Permission).OrderBy(p => p).ToList(),
            assignedCount);
    }

    public IReadOnlyList<string> GetAvailablePermissions() => Permissions.All;
}
