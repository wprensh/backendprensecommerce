using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Cart.GetCart;

/// <summary>GET /api/cart — carrito del usuario con precios vigentes del catálogo.</summary>
public static class GetCart
{
    public sealed record Item(Guid ProductId, string Name, string ImageUrl, decimal UnitPrice, int Quantity, decimal Subtotal);

    public sealed record Response(IReadOnlyList<Item> Items, int ItemCount, decimal Total);

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/cart", async (ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                    TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), ct)))
                .RequireAuthorization()
                .WithTags("Cart")
                .WithName("GetCart");
    }

    public sealed class Handler(CartStore carts, AppDbContext db) : IHandler
    {
        public async Task<Response> HandleAsync(Guid userId, CancellationToken ct)
        {
            var lines = await carts.GetAsync(userId, ct);
            var ids = lines.Select(l => l.ProductId).ToList();

            var products = await db.Products.AsNoTracking()
                .Where(p => ids.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, ct);

            // Productos borrados desde que se añadieron al carrito se omiten.
            var items = lines
                .Where(l => products.ContainsKey(l.ProductId))
                .Select(l =>
                {
                    var p = products[l.ProductId];
                    return new Item(p.Id, p.Name, p.ImageUrl, p.FinalPrice, l.Quantity, p.FinalPrice * l.Quantity);
                })
                .ToList();

            return new Response(items, items.Sum(i => i.Quantity), items.Sum(i => i.Subtotal));
        }
    }
}
