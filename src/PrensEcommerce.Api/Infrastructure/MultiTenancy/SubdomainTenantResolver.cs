using Microsoft.Extensions.Options;

namespace PrensEcommerce.Api.Infrastructure.MultiTenancy;

/// <summary>
/// Resuelve el tenant a partir del subdominio: <c>acme.prens.shop</c> → <c>acme</c>.
/// Solo acepta hosts bajo los dominios raíz configurados, para no confundir
/// "api" o "www" de dominios ajenos con un tenant.
/// </summary>
internal sealed class SubdomainTenantResolver(IOptions<MultiTenancyOptions> options) : ITenantResolver
{
    private static readonly HashSet<string> ReservedSubdomains =
        new(StringComparer.OrdinalIgnoreCase) { "www", "api", "admin", "app" };

    public string? ResolveIdentifier(HttpContext context)
    {
        var host = context.Request.Host.Host;
        if (string.IsNullOrEmpty(host))
        {
            return null;
        }

        foreach (var root in options.Value.RootDomains)
        {
            var suffix = "." + root.TrimStart('.');
            if (!host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var subdomain = host[..^suffix.Length];

            // Solo el primer nivel: "acme.prens.shop" sí, "x.acme.prens.shop" no.
            if (subdomain.Length == 0 || subdomain.Contains('.', StringComparison.Ordinal) ||
                ReservedSubdomains.Contains(subdomain))
            {
                return null;
            }

            return subdomain;
        }

        return null;
    }
}
