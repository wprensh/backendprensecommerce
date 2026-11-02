using System.Security.Claims;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Security;

namespace PrensEcommerce.Api.Features.Cart.RemoveCartItem;

/// <summary>DELETE /api/cart/items/{productId}</summary>
public static class RemoveCartItem
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapDelete("/cart/items/{productId:guid}", async (Guid productId, ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                {
                    await handler.HandleAsync(user.GetUserId(), productId, ct);
                    return TypedResults.NoContent();
                })
                .RequireAuthorization()
                .WithTags("Cart")
                .WithName("RemoveCartItem");
    }

    public sealed class Handler(CartStore carts) : IHandler
    {
        public async Task HandleAsync(Guid userId, Guid productId, CancellationToken ct)
        {
            var lines = await carts.GetAsync(userId, ct);
            lines.RemoveAll(l => l.ProductId == productId);
            await carts.SaveAsync(userId, lines, ct);
        }
    }
}
