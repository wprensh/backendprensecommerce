using Microsoft.Extensions.Options;

namespace PrensEcommerce.Api.Infrastructure.MultiTenancy;

/// <summary>Resuelve el tenant a partir del header <c>X-Tenant-ID</c> (configurable).</summary>
internal sealed class HeaderTenantResolver(IOptions<MultiTenancyOptions> options) : ITenantResolver
{
    public string? ResolveIdentifier(HttpContext context)
    {
        var value = context.Request.Headers[options.Value.HeaderName].ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
