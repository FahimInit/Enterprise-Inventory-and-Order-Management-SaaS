using Inventory.Domain.Common;

namespace Inventory.Domain.UnitTests.Common;

public sealed class TenantEntityTests
{
    private sealed class SampleTenantEntity : TenantEntityBase
    {
        public SampleTenantEntity(Guid tenantId) : base(tenantId)
        {
        }
    }

    [Fact]
    public void Constructor_WithEmptyTenantId_ShouldThrowDomainException()
    {
        var act = () => new SampleTenantEntity(Guid.Empty);

        act.Should().Throw<DomainException>()
            .Where(ex => ex.ErrorCode == "Tenant.Required");
    }

    [Fact]
    public void Constructor_WithValidTenantId_ShouldInitializeIdAndTimestamps()
    {
        var tenantId = Guid.CreateVersion7();

        var entity = new SampleTenantEntity(tenantId);

        entity.Id.Should().NotBeEmpty();
        entity.TenantId.Should().Be(tenantId);
        entity.CreatedAtUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }
}
