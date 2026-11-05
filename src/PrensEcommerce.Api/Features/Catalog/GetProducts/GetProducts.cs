using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Catalog.GetProducts;

/// <summary>
/// GET /api/products — listado paginado del catálogo con filtros y facetas.
/// <list type="bullet">
/// <item><c>category</c> / <c>categories</c>: slug o lista de slugs separados por comas.</item>
/// <item><c>brands</c>: slugs de marca separados por comas; <c>brandId</c> (administración).</item>
/// <item><c>minPrice</c>, <c>maxPrice</c>: sobre el precio final (con descuento).</item>
/// <item><c>minRating</c>, <c>onSale</c>, <c>inStock</c>, <c>favorites</c> (solo con sesión), <c>search</c>.</item>
/// <item><c>sort</c>: relevance | newest | price-asc | price-desc | rating | name.</item>
/// <item><c>includeFacets</c>: añade conteos por categoría y marca y el rango de precios.</item>
/// </list>
/// El filtro por tenant lo aplica el DbContext. La tienda solo ve productos activos;
/// <c>includeInactive</c> solo se respeta para quien gestiona el catálogo.
/// </summary>
public static class GetProducts
{
    public sealed record Query(
        string? Category, string? Categories, Guid? CategoryId, Guid? BrandId, string? Brands, string? Search, string? Sort,
        decimal? MinPrice = null, decimal? MaxPrice = null, decimal? MinRating = null,
        bool OnSale = false, bool InStock = false, bool Favorites = false, bool IncludeFacets = false,
        bool IncludeInactive = false, int Page = 1, int PageSize = 20);

    public sealed record FacetOption(string Slug, string Name, int Count);

    public sealed record Facets(
        IReadOnlyList<FacetOption> Categories, IReadOnlyList<FacetOption> Brands, decimal MinPrice, decimal MaxPrice);

    public sealed record Response(
        IReadOnlyList<ProductResponse> Items, int Page, int PageSize, int TotalCount, Facets? Facets = null);

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/products", async ([AsParameters] Query query, ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                    TypedResults.Ok(await handler.HandleAsync(
                        query with { IncludeInactive = query.IncludeInactive && user.IsCatalogManager() },
                        user.Identity?.IsAuthenticated == true ? user.GetUserId() : null,
                        ct)))
                .WithTags("Catalog")
                .WithName("GetProducts");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Response> HandleAsync(Query query, Guid? userId, CancellationToken ct)
        {
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            var visible = db.Products.AsNoTracking();
            if (!query.IncludeInactive)
            {
                visible = visible.Where(p => p.IsActive && p.Category!.IsActive);
            }

            var categories = SplitList(query.Categories, query.Category);
            var brands = SplitList(query.Brands);

            var products = Filter(visible, query, userId, categories, brands);
            products = Sort(products, query.Sort);

            var total = await products.CountAsync(ct);
            var items = await products
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(ProductResponse.Projection)
                .ToListAsync(ct);

            var facets = query.IncludeFacets ? await FacetsAsync(visible, query, userId, categories, brands, ct) : null;
            return new Response(items, page, pageSize, total, facets);
        }

        /// <summary>
        /// Cada faceta se cuenta con los demás filtros aplicados, pero no el suyo, para que
        /// los chips muestren cuántos productos habría al elegir otra opción.
        /// </summary>
        private async Task<Facets> FacetsAsync(
            IQueryable<Product> visible, Query query, Guid? userId,
            IReadOnlyList<string> categories, IReadOnlyList<string> brands, CancellationToken ct)
        {
            var categoryCounts = await Filter(visible, query, userId, [], brands)
                .GroupBy(p => new { p.Category!.Slug, p.Category.Name, p.Category.SortOrder })
                .Select(g => new { g.Key.Slug, g.Key.Name, g.Key.SortOrder, Count = g.Count() })
                .ToListAsync(ct);

            var brandCounts = await Filter(visible, query, userId, categories, [])
                .Where(p => p.Brand != null)
                .GroupBy(p => new { p.Brand!.Slug, p.Brand.Name })
                .Select(g => new { g.Key.Slug, g.Key.Name, Count = g.Count() })
                .ToListAsync(ct);

            var prices = await visible
                .Select(p => p.Price * (100 - p.DiscountPercent) / 100)
                .ToListAsync(ct);

            return new Facets(
                categoryCounts.OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
                    .Select(c => new FacetOption(c.Slug, c.Name, c.Count)).ToList(),
                brandCounts.OrderBy(b => b.Name).Select(b => new FacetOption(b.Slug, b.Name, b.Count)).ToList(),
                prices.Count == 0 ? 0 : Math.Floor(prices.Min()),
                prices.Count == 0 ? 0 : Math.Ceiling(prices.Max()));
        }

        private IQueryable<Product> Filter(
            IQueryable<Product> products, Query query, Guid? userId,
            IReadOnlyList<string> categories, IReadOnlyList<string> brands)
        {
            if (categories.Count > 0)
            {
                products = products.Where(p => categories.Contains(p.Category!.Slug));
            }

            if (query.CategoryId is { } categoryId)
            {
                products = products.Where(p => p.CategoryId == categoryId);
            }

            if (brands.Count > 0)
            {
                products = products.Where(p => p.Brand != null && brands.Contains(p.Brand.Slug));
            }

            if (query.BrandId is { } brandId)
            {
                products = products.Where(p => p.BrandId == brandId);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var pattern = $"%{query.Search.Trim()}%";
                products = products.Where(p => EF.Functions.ILike(p.Name, pattern) ||
                                               (p.Sku != null && EF.Functions.ILike(p.Sku, pattern)));
            }

            if (query.MinPrice is { } minPrice)
            {
                products = products.Where(p => p.Price * (100 - p.DiscountPercent) / 100 >= minPrice);
            }

            if (query.MaxPrice is { } maxPrice)
            {
                products = products.Where(p => p.Price * (100 - p.DiscountPercent) / 100 <= maxPrice);
            }

            if (query.MinRating is { } minRating)
            {
                products = products.Where(p => p.ReviewCount > 0 && p.Rating >= minRating);
            }

            if (query.OnSale)
            {
                products = products.Where(p => p.DiscountPercent > 0);
            }

            if (query.InStock)
            {
                products = products.Where(p => p.Stock > 0);
            }

            if (query.Favorites)
            {
                // Sin sesión no hay favoritos: lista vacía en lugar de error.
                products = userId is { } id
                    ? products.Where(p => db.WishlistItems.Any(w => w.UserId == id && w.ProductId == p.Id))
                    : products.Where(_ => false);
            }

            return products;
        }

        // Mismas opciones de orden que el frontend (ProductSort) + "name" para administración.
        private static IQueryable<Product> Sort(IQueryable<Product> products, string? sort) => sort switch
        {
            "price-asc" => products.OrderBy(p => p.Price * (100 - p.DiscountPercent)).ThenBy(p => p.Name),
            "price-desc" => products.OrderByDescending(p => p.Price * (100 - p.DiscountPercent)).ThenBy(p => p.Name),
            "rating" => products.OrderByDescending(p => p.Rating).ThenByDescending(p => p.ReviewCount).ThenBy(p => p.Name),
            "name" => products.OrderBy(p => p.Name),
            "newest" => products.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Name),
            _ => products.OrderByDescending(p => p.ReviewCount).ThenBy(p => p.Name),
        };

        private static List<string> SplitList(params string?[] values) =>
            values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .SelectMany(v => v!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Select(v => v.ToLowerInvariant())
                .Distinct()
                .ToList();
    }
}
