using Inventory.Domain.Common;

namespace Inventory.Domain.Suppliers;

public sealed class Supplier : TenantEntityBase
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? ContactName { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? AddressLine1 { get; private set; }
    public string? City { get; private set; }
    public string? Country { get; private set; }
    public string PaymentTerms { get; private set; } = "Net 30";
    public int LeadTimeDays { get; private set; } = 7;
    public bool IsActive { get; private set; } = true;
    public string? Notes { get; private set; }

    private readonly List<SupplierProduct> _products = [];
    public IReadOnlyCollection<SupplierProduct> Products => _products.AsReadOnly();

    private Supplier()
    {
    }

    public Supplier(
        Guid tenantId,
        string code,
        string name,
        string? contactName,
        string? email,
        string? phone,
        string? addressLine1,
        string? city,
        string? country,
        string paymentTerms = "Net 30",
        int leadTimeDays = 7,
        bool isActive = true,
        string? notes = null)
        : base(tenantId)
    {
        Update(
            code,
            name,
            contactName,
            email,
            phone,
            addressLine1,
            city,
            country,
            paymentTerms,
            leadTimeDays,
            isActive,
            notes);
    }

    public void Update(
        string code,
        string name,
        string? contactName,
        string? email,
        string? phone,
        string? addressLine1,
        string? city,
        string? country,
        string paymentTerms,
        int leadTimeDays,
        bool isActive,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("Supplier.CodeRequired", "Supplier code is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Supplier.NameRequired", "Supplier name is required.");
        }

        if (leadTimeDays < 0)
        {
            throw new DomainException("Supplier.InvalidLeadTime", "Lead time days cannot be negative.");
        }

        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        ContactName = string.IsNullOrWhiteSpace(contactName) ? null : contactName.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        AddressLine1 = string.IsNullOrWhiteSpace(addressLine1) ? null : addressLine1.Trim();
        City = string.IsNullOrWhiteSpace(city) ? null : city.Trim();
        Country = string.IsNullOrWhiteSpace(country) ? null : country.Trim();
        PaymentTerms = string.IsNullOrWhiteSpace(paymentTerms) ? "Net 30" : paymentTerms.Trim();
        LeadTimeDays = leadTimeDays;
        IsActive = isActive;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        TouchUpdatedTimestamp();
    }
}

public sealed class SupplierProduct : TenantEntityBase
{
    public Guid SupplierId { get; private set; }
    public Guid ProductId { get; private set; }
    public string? SupplierSku { get; private set; }
    public decimal AgreedUnitCost { get; private set; }
    public int MinimumOrderQuantity { get; private set; } = 1;
    public int LeadTimeDays { get; private set; } = 7;
    public bool IsPreferred { get; private set; }

    private SupplierProduct()
    {
    }

    public SupplierProduct(
        Guid tenantId,
        Guid supplierId,
        Guid productId,
        string? supplierSku,
        decimal agreedUnitCost,
        int minimumOrderQuantity = 1,
        int leadTimeDays = 7,
        bool isPreferred = false)
        : base(tenantId)
    {
        if (supplierId == Guid.Empty)
        {
            throw new DomainException("SupplierProduct.SupplierRequired", "SupplierId is required.");
        }

        if (productId == Guid.Empty)
        {
            throw new DomainException("SupplierProduct.ProductRequired", "ProductId is required.");
        }

        SupplierId = supplierId;
        ProductId = productId;
        UpdateTerms(supplierSku, agreedUnitCost, minimumOrderQuantity, leadTimeDays, isPreferred);
    }

    public void UpdateTerms(
        string? supplierSku,
        decimal agreedUnitCost,
        int minimumOrderQuantity,
        int leadTimeDays,
        bool isPreferred)
    {
        if (agreedUnitCost < 0m)
        {
            throw new DomainException("SupplierProduct.NegativeCost", "Agreed unit cost cannot be negative.");
        }

        if (minimumOrderQuantity < 1)
        {
            throw new DomainException("SupplierProduct.InvalidMoq", "Minimum order quantity must be at least 1.");
        }

        if (leadTimeDays < 0)
        {
            throw new DomainException("SupplierProduct.InvalidLeadTime", "Lead time days cannot be negative.");
        }

        SupplierSku = string.IsNullOrWhiteSpace(supplierSku) ? null : supplierSku.Trim().ToUpperInvariant();
        AgreedUnitCost = decimal.Round(agreedUnitCost, 4);
        MinimumOrderQuantity = minimumOrderQuantity;
        LeadTimeDays = leadTimeDays;
        IsPreferred = isPreferred;
        TouchUpdatedTimestamp();
    }
}
