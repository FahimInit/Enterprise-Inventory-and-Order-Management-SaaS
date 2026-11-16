using Inventory.Domain.Common;
using Inventory.Domain.Identity;

namespace Inventory.Domain.Organizations;

public sealed class OrganizationRole : TenantEntityBase
{
    private readonly List<RolePermission> _permissions = [];

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsSystemRole { get; private set; }

    public IReadOnlyCollection<RolePermission> Permissions => _permissions.AsReadOnly();

    private OrganizationRole()
    {
    }

    public OrganizationRole(
        Guid tenantId,
        string name,
        string? description,
        bool isSystemRole,
        IEnumerable<string> permissions)
        : base(tenantId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Role.NameRequired", "Role name is required.");
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        IsSystemRole = isSystemRole;
        SetPermissions(permissions);
    }

    public void UpdateRole(string name, string? description, IEnumerable<string> permissions)
    {
        if (IsSystemRole && !string.Equals(Name, name.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("Role.SystemRoleRenameForbidden", "System roles cannot be renamed.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Role.NameRequired", "Role name is required.");
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        SetPermissions(permissions);
        TouchUpdatedTimestamp();
    }

    public void SetPermissions(IEnumerable<string> permissions)
    {
        var validSet = Identity.Permissions.All.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var distinctPermissions = permissions
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var perm in distinctPermissions)
        {
            if (!validSet.Contains(perm))
            {
                throw new DomainException("Role.UnknownPermission", $"Permission '{perm}' is not a recognized platform permission.");
            }
        }

        _permissions.Clear();
        foreach (var perm in distinctPermissions)
        {
            _permissions.Add(new RolePermission(TenantId, Id, perm));
        }
    }
}

public sealed class RolePermission : EntityBase, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid RoleId { get; private set; }
    public string Permission { get; private set; } = string.Empty;

    private RolePermission()
    {
    }

    public RolePermission(Guid tenantId, Guid roleId, string permission)
    {
        TenantId = tenantId;
        RoleId = roleId;
        Permission = permission.Trim().ToLowerInvariant();
    }
}
