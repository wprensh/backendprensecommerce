using System.Linq.Expressions;
using PrensEcommerce.Api.Domain;

namespace PrensEcommerce.Api.Features.Catalog;

/// <summary>Contrato compartido por los slices de lectura del catálogo (coincide con <c>Product</c> del frontend).</summary>
public sealed record ProductResponse(
    Guid Id, string Name, string Description, decimal Price, int DiscountPercent,
    string Category, string ImageUrl, decimal Rating, int ReviewCount, int Stock)
{
    /// <summary>Proyección traducible a SQL, para no materializar la entidad completa.</summary>
    public static readonly Expression<Func<Product, ProductResponse>> Projection = p =>
        new ProductResponse(p.Id, p.Name, p.Description, p.Price, p.DiscountPercent,
            p.Category, p.ImageUrl, p.Rating, p.ReviewCount, p.Stock);
}
