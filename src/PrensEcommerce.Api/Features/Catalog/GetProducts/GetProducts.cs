using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Catalog.GetProducts;

/// <summary>
/// GET /api/products?category=&amp;brandId=&amp;search=&amp;sort=&amp;includeInactive=&amp;page=&amp;pageSize=
/// El filtro por tenant lo aplica el DbContext; el slice no necesita conocerlo.
/// La tienda solo ve productos activos; <c>includeInactive</c> solo se respeta con sesión iniciada (administración).
/// </summary>
public static class GetProducts
{
    public sealed record Query(
        string? Category, Guid? CategoryId, Guid? BrandId, string? Search, string? Sort,
        bool IncludeInactive = false, int Page = 1, int PageSize = 20);

    public sealed record Response(IReadOnlyList<ProductResponse> Items, int Page, int PageSize, int TotalCount);

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/products", async ([AsParameters] Query query, ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                    TypedResults.Ok(await handler.HandleAsync(
                        query with { IncludeInactive = query.IncludeInactive && user.IsCatalogManager() }, ct)))
                .WithTags("Catalog")
                .WithName("GetProducts");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Response> HandleAsync(Query query, CancellationToken ct)
        {
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            var products = db.Products.AsNoTracking();

            if (!query.IncludeInactive)
            {
                products = products.Where(p => p.IsActive && p.Category!.IsActive);
            }

            if (!string.IsNullOrWhiteSpace(query.Category))
            {
                products = products.Where(p => p.Category!.Slug == query.Category);
            }

            if (query.CategoryId is { } categoryId)
            {
                products = products.Where(p => p.CategoryId == categoryId);
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

            // Mismas opciones de orden que el frontend (ProductSort) + "name" y "newest" para administración.
            products = query.Sort switch
            {
                "price-asc" => products.OrderBy(p => p.Price * (100 - p.DiscountPercent)),
                "price-desc" => products.OrderByDescending(p => p.Price * (100 - p.DiscountPercent)),
                "rating" => products.OrderByDescending(p => p.Rating).ThenByDescending(p => p.ReviewCount),
                "name" => products.OrderBy(p => p.Name),
                "newest" => products.OrderByDescending(p => p.CreatedAt),
                _ => products.OrderByDescending(p => p.ReviewCount).ThenBy(p => p.Name),
            };

            var total = await products.CountAsync(ct);
            var items = await products
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(ProductResponse.Projection)
                .ToListAsync(ct);

            return new Response(items, page, pageSize, total);
        }
    }
}
