namespace PrensEcommerce.Api.Infrastructure.Persistence;

/// <summary>
/// Esquemas de PostgreSQL por módulo de negocio. Todos comparten la misma base de datos y el
/// mismo discriminador <c>tenant_id</c>: el esquema agrupa por dominio, no aísla tenants.
/// </summary>
public static class Schemas
{
    public const string Core = "core";
    public const string Identity = "identity";
    public const string Catalog = "catalog";
    public const string Sales = "sales";
    public const string Support = "support";
    public const string Crm = "crm";
}
