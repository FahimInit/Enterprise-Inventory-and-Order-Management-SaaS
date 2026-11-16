using Inventory.Domain.Common;
using Inventory.Domain.Organizations;

namespace Inventory.Domain.Catalog;

public sealed class Category : TenantEntityBase
{
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid? ParentCategoryId { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Category()
    {
    }

    public Category(
        Guid tenantId,
        string name,
        string? slug = null,
        string? description = null,
        Guid? parentCategoryId = null)
        : base(tenantId)
    {
        Update(name, slug, description, parentCategoryId, isActive: true);
    }

    public void Update(
        string name,
        string? slug,
        string? description,
        Guid? parentCategoryId,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Category.NameRequired", "Category name is required.");
        }

        if (parentCategoryId.HasValue && parentCategoryId.Value == Id)
        {
            throw new DomainException("Category.SelfParentForbidden", "A category cannot be its own parent category.");
        }

        var normalizedSlug = string.IsNullOrWhiteSpace(slug)
            ? Organization.NormalizeSlug(name)
            : Organization.NormalizeSlug(slug);

        if (string.IsNullOrWhiteSpace(normalizedSlug))
        {
            throw new DomainException("Category.InvalidSlug", "Category slug is invalid.");
        }

        Name = name.Trim();
        Slug = normalizedSlug;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        ParentCategoryId = parentCategoryId;
        IsActive = isActive;
        TouchUpdatedTimestamp();
    }
}
