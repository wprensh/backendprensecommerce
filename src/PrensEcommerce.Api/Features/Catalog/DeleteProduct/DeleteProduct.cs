using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Catalog.DeleteProduct;

/// <summary>
/// DELETE /api/products/{id}. Si el producto ya aparece en pedidos no se borra (409):
/// hay que desactivarlo para conservar el histórico.
/// </summary>
public static class DeleteProduct
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapDelete("/products/{id:guid}", async (Guid id, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(id, ct)).Match(_ => TypedResults.NoContent()))
                .RequireCatalogManager()
                .WithTags("Catalog")
                .WithName("DeleteProduct")
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict);
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<bool>> HandleAsync(Guid id, CancellationToken ct)
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (product is null)
            {
                return Error.NotFound("Product.NotFound", $"No existe el producto {id}.");
            }

            if (await db.Set<OrderItem>().AnyAsync(i => i.ProductId == id, ct))
            {
                return Error.Conflict("Product.HasOrders",
                    "El producto tiene pedidos asociados. Desactívalo en lugar de eliminarlo.");
            }

            db.Products.Remove(product);
            await db.SaveChangesAsync(ct);
            return true;
        }
    }
}
