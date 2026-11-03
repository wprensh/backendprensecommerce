using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Features.Catalog;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Wishlist;

/// <summary>GET /api/wishlist — productos favoritos del cliente, los últimos añadidos primero.</summary>
public static class GetWishlist
{
    public sealed record Item(ProductResponse Product, DateTimeOffset AddedAt);

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/wishlist", async (ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                    TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), ct)))
                .RequireAuthorization()
                .WithTags("Wishlist")
                .WithName("GetWishlist");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<IReadOnlyList<Item>> HandleAsync(Guid userId, CancellationToken ct)
        {
            var rows = await db.WishlistItems.AsNoTracking()
                .Where(w => w.UserId == userId && w.Product!.IsActive)
                .OrderByDescending(w => w.CreatedAt)
                .Select(w => new { w.ProductId, w.CreatedAt })
                .ToListAsync(ct);

            var ids = rows.Select(r => r.ProductId).ToList();
            var products = await db.Products.AsNoTracking()
                .Where(p => ids.Contains(p.Id))
                .Select(ProductResponse.Projection)
                .ToDictionaryAsync(p => p.Id, ct);

            return rows.Where(r => products.ContainsKey(r.ProductId))
                .Select(r => new Item(products[r.ProductId], r.CreatedAt))
                .ToList();
        }
    }
}

/// <summary>PUT /api/wishlist/{productId} — añade un favorito (idempotente).</summary>
public static class AddToWishlist
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPut("/wishlist/{productId:guid}", async (Guid productId, ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(user.GetUserId(), productId, ct)).Match(_ => TypedResults.NoContent()))
                .RequireAuthorization()
                .WithTags("Wishlist")
                .WithName("AddToWishlist");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<bool>> HandleAsync(Guid userId, Guid productId, CancellationToken ct)
        {
            if (!await db.Products.AnyAsync(p => p.Id == productId && p.IsActive, ct))
            {
                return Error.NotFound("Wishlist.ProductNotFound", "El producto no existe.");
            }

            if (!await db.WishlistItems.AnyAsync(w => w.UserId == userId && w.ProductId == productId, ct))
            {
                db.WishlistItems.Add(new WishlistItem { UserId = userId, ProductId = productId });
                try
                {
                    await db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException)
                {
                    // Doble clic: otra petición lo insertó a la vez; el resultado es el mismo.
                }
            }

            return true;
        }
    }
}

/// <summary>DELETE /api/wishlist/{productId} — quita un favorito (idempotente).</summary>
public static class RemoveFromWishlist
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapDelete("/wishlist/{productId:guid}", async (Guid productId, ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                {
                    await handler.HandleAsync(user.GetUserId(), productId, ct);
                    return TypedResults.NoContent();
                })
                .RequireAuthorization()
                .WithTags("Wishlist")
                .WithName("RemoveFromWishlist");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public Task<int> HandleAsync(Guid userId, Guid productId, CancellationToken ct) =>
            db.WishlistItems.Where(w => w.UserId == userId && w.ProductId == productId).ExecuteDeleteAsync(ct);
    }
}
