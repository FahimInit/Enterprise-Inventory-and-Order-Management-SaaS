using System.Text.RegularExpressions;
using Inventory.Domain.Common;

namespace Inventory.Domain.Organizations;

public sealed class Organization : EntityBase
{
    private static readonly Regex SlugPattern = new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);

    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string BaseCurrency { get; private set; } = "USD";
    public string TimeZoneId { get; private set; } = "UTC";
    public int DefaultLowStockThreshold { get; private set; } = 10;
    public decimal DefaultTaxRatePercent { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Organization()
    {
    }

    public Organization(
        string name,
        string slug,
        string? description = null,
        string baseCurrency = "USD",
        string timeZoneId = "UTC",
        int defaultLowStockThreshold = 10,
        decimal defaultTaxRatePercent = 0m)
    {
        UpdateSettings(
            name,
            slug,
            description,
            baseCurrency,
            timeZoneId,
            defaultLowStockThreshold,
            defaultTaxRatePercent);
        IsActive = true;
    }

    public void UpdateSettings(
        string name,
        string slug,
        string? description,
        string baseCurrency,
        string timeZoneId,
        int defaultLowStockThreshold,
        decimal defaultTaxRatePercent)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Organization.NameRequired", "Organization name is required.");
        }

        var normalizedSlug = NormalizeSlug(slug);
        if (string.IsNullOrWhiteSpace(normalizedSlug) || !SlugPattern.IsMatch(normalizedSlug))
        {
            throw new DomainException("Organization.InvalidSlug", "Organization slug must contain only lowercase letters, numbers, and hyphens.");
        }

        if (defaultLowStockThreshold < 0)
        {
            throw new DomainException("Organization.InvalidLowStockThreshold", "Default low-stock threshold cannot be negative.");
        }

        if (defaultTaxRatePercent < 0m || defaultTaxRatePercent > 100m)
        {
            throw new DomainException("Organization.InvalidTaxRate", "Default tax rate percent must be between 0 and 100.");
        }

        Name = name.Trim();
        Slug = normalizedSlug;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        BaseCurrency = string.IsNullOrWhiteSpace(baseCurrency) ? "USD" : baseCurrency.Trim().ToUpperInvariant();
        TimeZoneId = string.IsNullOrWhiteSpace(timeZoneId) ? "UTC" : timeZoneId.Trim();
        DefaultLowStockThreshold = defaultLowStockThreshold;
        DefaultTaxRatePercent = decimal.Round(defaultTaxRatePercent, 4);
        TouchUpdatedTimestamp();
    }

    public static string NormalizeSlug(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var lower = input.Trim().ToLowerInvariant();
        var hyphenated = Regex.Replace(lower, @"[^a-z0-9]+", "-").Trim('-');
        return hyphenated;
    }
}
