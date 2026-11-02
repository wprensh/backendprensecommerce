namespace PrensEcommerce.Api.Domain;

/// <summary>Producto del catálogo. Alineado con <c>Product</c> del frontend Angular.</summary>
public sealed class Product : TenantEntity
{
    public required string Name { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }

    /// <summary>Porcentaje de descuento (0–100).</summary>
    public int DiscountPercent { get; set; }

    /// <summary>Slug de categoría: audio, hogar, moda, deportes…</summary>
    public required string Category { get; set; }

    public string ImageUrl { get; set; } = string.Empty;

    public decimal Rating { get; set; }

    public int ReviewCount { get; set; }

    public int Stock { get; set; }

    public decimal FinalPrice => Math.Round(Price * (100 - DiscountPercent) / 100m, 2);
}
