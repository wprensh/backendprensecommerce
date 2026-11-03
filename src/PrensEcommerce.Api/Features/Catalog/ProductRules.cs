using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Catalog;

/// <summary>Datos editables de un producto, comunes al alta y a la modificación.</summary>
public interface IProductData
{
    string Name { get; }
    string? Sku { get; }
    string? Description { get; }
    decimal Price { get; }
    int DiscountPercent { get; }
    Guid CategoryId { get; }
    Guid? BrandId { get; }
    string? ImageUrl { get; }
    /// <summary>Galería ordenada; si no se envía, se usa <see cref="ImageUrl"/> como única imagen.</summary>
    IReadOnlyList<string>? Images { get; }
    int Stock { get; }
}

internal sealed class ProductDataValidator<T> : AbstractValidator<T> where T : IProductData
{
    public ProductDataValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Sku).MaximumLength(64);
        RuleFor(c => c.Description).MaximumLength(4000);
        RuleFor(c => c.Price).GreaterThan(0);
        RuleFor(c => c.DiscountPercent).InclusiveBetween(0, 100);
        RuleFor(c => c.CategoryId).NotEmpty();
        RuleFor(c => c.ImageUrl).MaximumLength(1000);
        RuleFor(c => c.Images).Must(i => i is null || i.Count <= ProductRules.MaxImages)
            .WithMessage($"Un producto admite como máximo {ProductRules.MaxImages} imágenes.");
        RuleForEach(c => c.Images).NotEmpty().MaximumLength(1000);
        RuleFor(c => c.Stock).GreaterThanOrEqualTo(0);
    }
}

/// <summary>Reglas de negocio que necesitan base de datos (las filtra el tenant automáticamente).</summary>
internal static class ProductRules
{
    public static async Task<Error?> CheckReferencesAsync(AppDbContext db, IProductData data, Guid? productId, CancellationToken ct)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == data.CategoryId, ct))
        {
            return Error.Validation("Product.CategoryNotFound", "La categoría indicada no existe.");
        }

        if (data.BrandId is { } brandId && !await db.Brands.AnyAsync(b => b.Id == brandId, ct))
        {
            return Error.Validation("Product.BrandNotFound", "La marca indicada no existe.");
        }

        var sku = NormalizeSku(data.Sku);
        if (sku is not null && await db.Products.AnyAsync(p => p.Sku == sku && p.Id != productId, ct))
        {
            return Error.Conflict("Product.DuplicateSku", $"Ya existe un producto con el SKU {sku}.");
        }

        return null;
    }

    public const int MaxImages = 10;

    /// <summary>Galería limpia (sin vacíos ni duplicados, en el orden recibido) y su imagen principal.</summary>
    public static (List<string> Images, string Main) NormalizeImages(IProductData data)
    {
        var images = (data.Images ?? (string.IsNullOrWhiteSpace(data.ImageUrl) ? [] : [data.ImageUrl]))
            .Select(i => i.Trim())
            .Where(i => i.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Take(MaxImages)
            .ToList();
        return (images, images.FirstOrDefault() ?? string.Empty);
    }

    public static string? NormalizeSku(string? sku) =>
        string.IsNullOrWhiteSpace(sku) ? null : sku.Trim().ToUpperInvariant();
}
