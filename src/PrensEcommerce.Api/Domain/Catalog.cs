namespace PrensEcommerce.Api.Domain;

// ── Esquema "catalog": categorías, marcas y productos ─────────────────────────

/// <summary>Categoría del catálogo. Admite jerarquía mediante <see cref="ParentId"/>.</summary>
public sealed class Category : TenantEntity
{
    public required string Name { get; set; }

    /// <summary>Identificador en URL (ej. "audio"). Único por tienda.</summary>
    public required string Slug { get; set; }

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public Guid? ParentId { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>Marca o fabricante de productos de una tienda.</summary>
public sealed class Brand : TenantEntity
{
    public required string Name { get; set; }

    /// <summary>Identificador en URL (ej. "sony"). Único por tienda.</summary>
    public required string Slug { get; set; }

    public string? LogoUrl { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>Producto del catálogo. Alineado con <c>Product</c> del frontend Angular.</summary>
public sealed class Product : TenantEntity
{
    /// <summary>Código interno. Único por tienda cuando se informa.</summary>
    public string? Sku { get; set; }

    public required string Name { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }

    /// <summary>Porcentaje de descuento (0–100).</summary>
    public int DiscountPercent { get; set; }

    public Guid CategoryId { get; set; }

    public Category? Category { get; set; }

    public Guid? BrandId { get; set; }

    public Brand? Brand { get; set; }

    public string ImageUrl { get; set; } = string.Empty;

    public decimal Rating { get; set; }

    public int ReviewCount { get; set; }

    public int Stock { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Precio con descuento, redondeado a pesos enteros (COP no usa decimales).</summary>
    public decimal FinalPrice => Math.Round(Price * (100 - DiscountPercent) / 100m, 0, MidpointRounding.AwayFromZero);
}
