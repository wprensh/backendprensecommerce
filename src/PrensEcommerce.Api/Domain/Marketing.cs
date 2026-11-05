namespace PrensEcommerce.Api.Domain;

// ── Campañas de marketing (esquema "crm") ─────────────────────────────────────

/// <summary>Estilo de color de la tarjeta del banner.</summary>
public enum CampaignTheme
{
    /// <summary>Tarjeta blanca con flecha azul (diseño 1c por defecto).</summary>
    Light = 0,

    /// <summary>Tarjeta azul institucional con texto blanco.</summary>
    Navy = 1,

    /// <summary>Tarjeta amarilla institucional.</summary>
    Yellow = 2,
}

/// <summary>
/// Campaña comercial de la tienda: define el banner principal del catálogo durante su vigencia.
/// Si varias están vigentes a la vez se muestra la de mayor <see cref="Priority"/>.
/// </summary>
public sealed class Campaign : TenantEntity
{
    /// <summary>Nombre interno (solo administración), p. ej. «Black Friday 2026».</summary>
    public required string Name { get; set; }

    /// <summary>Texto pequeño sobre el título del banner, p. ej. «Nueva colección».</summary>
    public string Eyebrow { get; set; } = string.Empty;

    public required string Title { get; set; }

    public string? Subtitle { get; set; }

    /// <summary>Texto del botón; sin él solo se muestra la flecha.</summary>
    public string? CtaLabel { get; set; }

    public string? ImageUrl { get; set; }

    /// <summary>Texto alternativo de la imagen (y rótulo cuando no hay imagen).</summary>
    public string? ImageAlt { get; set; }

    /// <summary>Slug de la categoría que abre el banner; null abre todo el catálogo.</summary>
    public string? CategorySlug { get; set; }

    public CampaignTheme Theme { get; set; } = CampaignTheme.Light;

    /// <summary>Inicio de la vigencia; null = desde ya.</summary>
    public DateTimeOffset? StartsAt { get; set; }

    /// <summary>Fin de la vigencia (exclusivo); null = sin fecha de fin.</summary>
    public DateTimeOffset? EndsAt { get; set; }

    public int Priority { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsLiveAt(DateTimeOffset now) =>
        IsActive && (StartsAt is null || StartsAt <= now) && (EndsAt is null || EndsAt > now);
}
