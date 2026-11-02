namespace PrensEcommerce.Api.Infrastructure.MultiTenancy;

public sealed class MultiTenancyOptions
{
    public const string SectionName = "MultiTenancy";

    /// <summary>Nombre del header HTTP con el identificador (slug o Guid) del tenant.</summary>
    public string HeaderName { get; set; } = "X-Tenant-ID";

    /// <summary>
    /// Dominios raíz para resolver por subdominio. Ej.: "prens.shop" hace que
    /// "acme.prens.shop" resuelva al tenant "acme".
    /// </summary>
    public IList<string> RootDomains { get; } = [];

    /// <summary>Rutas que no requieren tenant (prefijos).</summary>
    public IList<string> ExcludedPaths { get; } = [];

    /// <summary>Minutos que se cachea en Redis la búsqueda de un tenant.</summary>
    public int CacheMinutes { get; set; } = 10;
}
