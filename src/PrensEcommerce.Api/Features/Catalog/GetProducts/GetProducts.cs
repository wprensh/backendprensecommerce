using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Catalog.GetProducts;

/// <summary>
/// GET /api/products?category=&amp;search=&amp;sort=&amp;page=&amp;pageSize=
/// El filtro por tenant lo aplica el DbContext; el slice no necesita conocerlo.
/// </summary>
public static class GetProducts
{
    public sealed record Query(string? Category, string? Search, string? Sort, int Page = 1, int PageSize = 20);

    public sealed record Response(IReadOnlyList<ProductResponse> Items, int Page, int PageSize, int TotalCount);

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/products", async ([AsParameters] Query query, Handler handler, CancellationToken ct) =>
                    TypedResults.Ok(await handler.HandleAsync(query, ct)))
                .WithTags("Catalog")
                .WithName("GetProducts");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Response> HandleAsync(Query query, CancellationToken ct)
        {
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            var products = db.Products.AsNoTracking().Where(p => p.IsActive);

            if (!string.IsNullOrWhiteSpace(query.Category))
            {
                products = products.Where(p => p.Category!.Slug == query.Category);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var pattern = $"%{query.Search.Trim()}%";
                products = products.Where(p => EF.Functions.ILike(p.Name, pattern));
            }

            // Mismas opciones de orden que el frontend (ProductSort).
            products = query.Sort switch
            {
                "price-asc" => products.OrderBy(p => p.Price * (100 - p.DiscountPercent)),
                "price-desc" => products.OrderByDescending(p => p.Price * (100 - p.DiscountPercent)),
                "rating" => products.OrderByDescending(p => p.Rating).ThenByDescending(p => p.ReviewCount),
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
