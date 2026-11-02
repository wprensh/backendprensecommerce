using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Catalog.GetProductById;

/// <summary>GET /api/products/{id}</summary>
public static class GetProductById
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/products/{id:guid}", async (Guid id, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(id, ct)).Match(TypedResults.Ok))
                .WithTags("Catalog")
                .WithName("GetProductById")
                .Produces<ProductResponse>()
                .ProducesProblem(StatusCodes.Status404NotFound);
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<ProductResponse>> HandleAsync(Guid id, CancellationToken ct)
        {
            var product = await db.Products.AsNoTracking()
                .Where(p => p.Id == id)
                .Select(ProductResponse.Projection)
                .FirstOrDefaultAsync(ct);

            return product is null
                ? Error.NotFound("Product.NotFound", $"No existe el producto {id}.")
                : product;
        }
    }
}
