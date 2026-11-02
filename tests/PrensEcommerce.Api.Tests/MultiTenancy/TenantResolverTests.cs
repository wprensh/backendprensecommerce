using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PrensEcommerce.Api.Infrastructure.MultiTenancy;

namespace PrensEcommerce.Api.Tests.MultiTenancy;

public class TenantResolverTests
{
    private static IOptions<MultiTenancyOptions> Options()
    {
        var options = new MultiTenancyOptions();
        options.RootDomains.Add("prens.shop");
        return Microsoft.Extensions.Options.Options.Create(options);
    }

    private static DefaultHttpContext Request(string host, string? tenantHeader = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);
        if (tenantHeader is not null)
        {
            context.Request.Headers["X-Tenant-ID"] = tenantHeader;
        }

        return context;
    }

    [Theory]
    [InlineData("acme.prens.shop", "acme")]
    [InlineData("ACME.Prens.Shop", "ACME")]
    [InlineData("prens.shop", null)]
    [InlineData("www.prens.shop", null)]
    [InlineData("api.prens.shop", null)]
    [InlineData("x.acme.prens.shop", null)]
    [InlineData("acme.otrodominio.com", null)]
    [InlineData("evilprens.shop", null)]
    public void Subdomain_resolver_extracts_first_level_subdomain(string host, string? expected)
    {
        var resolver = new SubdomainTenantResolver(Options());

        Assert.Equal(expected, resolver.ResolveIdentifier(Request(host)));
    }

    [Theory]
    [InlineData("acme", "acme")]
    [InlineData("  acme  ", "acme")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Header_resolver_reads_X_Tenant_ID(string? header, string? expected)
    {
        var resolver = new HeaderTenantResolver(Options());

        Assert.Equal(expected, resolver.ResolveIdentifier(Request("localhost", header)));
    }
}
