using Inventory.Domain.Common;

namespace Inventory.Domain.Warehouses;

public sealed class Warehouse : TenantEntityBase
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? AddressLine1 { get; private set; }
    public string? City { get; private set; }
    public string? StateOrProvince { get; private set; }
    public string? PostalCode { get; private set; }
    public string? Country { get; private set; }
    public Guid? ManagerUserId { get; private set; }
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; } = true;

    private readonly List<WarehouseLocation> _locations = [];
    public IReadOnlyCollection<WarehouseLocation> Locations => _locations.AsReadOnly();

    private Warehouse()
    {
    }

    public Warehouse(
        Guid tenantId,
        string code,
        string name,
        string? addressLine1,
        string? city,
        string? stateOrProvince,
        string? postalCode,
        string? country,
        Guid? managerUserId = null,
        bool isDefault = false,
        bool isActive = true)
        : base(tenantId)
    {
        Update(
            code,
            name,
            addressLine1,
            city,
            stateOrProvince,
            postalCode,
            country,
            managerUserId,
            isDefault,
            isActive);
    }

    public void Update(
        string code,
        string name,
        string? addressLine1,
        string? city,
        string? stateOrProvince,
        string? postalCode,
        string? country,
        Guid? managerUserId,
        bool isDefault,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("Warehouse.CodeRequired", "Warehouse code is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Warehouse.NameRequired", "Warehouse name is required.");
        }

        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        AddressLine1 = string.IsNullOrWhiteSpace(addressLine1) ? null : addressLine1.Trim();
        City = string.IsNullOrWhiteSpace(city) ? null : city.Trim();
        StateOrProvince = string.IsNullOrWhiteSpace(stateOrProvince) ? null : stateOrProvince.Trim();
        PostalCode = string.IsNullOrWhiteSpace(postalCode) ? null : postalCode.Trim();
        Country = string.IsNullOrWhiteSpace(country) ? null : country.Trim();
        ManagerUserId = managerUserId;
        IsDefault = isDefault;
        IsActive = isActive;
        TouchUpdatedTimestamp();
    }
}

public sealed class WarehouseLocation : TenantEntityBase
{
    public Guid WarehouseId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Zone { get; private set; } = "GENERAL";
    public bool IsPickable { get; private set; } = true;
    public bool IsReceivable { get; private set; } = true;
    public bool IsActive { get; private set; } = true;

    private WarehouseLocation()
    {
    }

    public WarehouseLocation(
        Guid tenantId,
        Guid warehouseId,
        string code,
        string name,
        string zone = "GENERAL",
        bool isPickable = true,
        bool isReceivable = true,
        bool isActive = true)
        : base(tenantId)
    {
        if (warehouseId == Guid.Empty)
        {
            throw new DomainException("WarehouseLocation.WarehouseRequired", "WarehouseId is required.");
        }

        WarehouseId = warehouseId;
        Update(code, name, zone, isPickable, isReceivable, isActive);
    }

    public void Update(
        string code,
        string name,
        string zone,
        bool isPickable,
        bool isReceivable,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("WarehouseLocation.CodeRequired", "Location code is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("WarehouseLocation.NameRequired", "Location name is required.");
        }

        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        Zone = string.IsNullOrWhiteSpace(zone) ? "GENERAL" : zone.Trim().ToUpperInvariant();
        IsPickable = isPickable;
        IsReceivable = isReceivable;
        IsActive = isActive;
        TouchUpdatedTimestamp();
    }
}
