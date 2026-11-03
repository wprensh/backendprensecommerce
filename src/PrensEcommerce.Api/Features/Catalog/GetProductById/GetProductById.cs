using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Catalog.GetProductById;

/// <summary>GET /api/products/{id} — los gestores del catálogo también ven productos inactivos.</summary>
public static class GetProductById
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/products/{id:guid}", async (Guid id, System.Security.Claims.ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(id, user.IsCatalogManager(), ct)).Match(TypedResults.Ok))
                .WithTags("Catalog")
                .WithName("GetProductById")
                .Produces<ProductResponse>()
                .ProducesProblem(StatusCodes.Status404NotFound);
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<ProductResponse>> HandleAsync(Guid id, bool includeInactive, CancellationToken ct)
        {
            var product = await db.Products.AsNoTracking()
                .Where(p => p.Id == id && (includeInactive || p.IsActive))
                .Select(ProductResponse.Projection)
                .FirstOrDefaultAsync(ct);

            return product is null
                ? Error.NotFound("Product.NotFound", $"No existe el producto {id}.")
                : product;
        }
    }
}
