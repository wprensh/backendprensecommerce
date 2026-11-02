using PrensEcommerce.Api.Infrastructure.MultiTenancy;

namespace PrensEcommerce.Api.Tests.MultiTenancy;

public class TenantProviderTests
{
    private static readonly TenantInfo Acme = new(Guid.NewGuid(), "acme", "Acme");

    [Fact]
    public void Without_tenant_TenantId_throws()
    {
        var provider = new TenantProvider();

        Assert.False(provider.HasTenant);
        Assert.Throws<InvalidOperationException>(() => provider.TenantId);
    }

    [Fact]
    public void SetTenant_exposes_tenant_and_rejects_a_different_one()
    {
        var provider = new TenantProvider();
        provider.SetTenant(Acme);

        Assert.Equal(Acme.Id, provider.TenantId);
        provider.SetTenant(Acme); // idempotente
        Assert.Throws<InvalidOperationException>(() =>
            provider.SetTenant(new TenantInfo(Guid.NewGuid(), "otra", "Otra")));
    }
}
