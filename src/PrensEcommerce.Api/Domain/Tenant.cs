namespace PrensEcommerce.Api.Domain;

/// <summary>Tienda (tenant). Tabla global: no lleva filtro por tenant.</summary>
public sealed class Tenant : Entity
{
    /// <summary>Identificador corto usado en subdominio y header (ej. "acme").</summary>
    public required string Slug { get; set; }

    public required string Name { get; set; }

    public bool IsActive { get; set; } = true;
}
