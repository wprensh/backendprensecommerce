using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Orders.GetOrderById;

/// <summary>GET /api/orders/{id} — detalle de un pedido del usuario (página de confirmación).</summary>
public static class GetOrderById
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/orders/{id:guid}", async (Guid id, ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(user.GetUserId(), id, ct)).Match(TypedResults.Ok))
                .RequireAuthorization()
                .WithTags("Orders")
                .WithName("GetOrderById")
                .Produces<OrderResponse>()
                .ProducesProblem(StatusCodes.Status404NotFound);
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<OrderResponse>> HandleAsync(Guid userId, Guid orderId, CancellationToken ct)
        {
            // Filtrar por usuario evita revelar pedidos ajenos (404, no 403).
            var order = await db.Orders.AsNoTracking()
                .Where(o => o.Id == orderId && o.UserId == userId)
                .Select(OrderResponse.Projection)
                .FirstOrDefaultAsync(ct);

            return order is null
                ? Error.NotFound("Order.NotFound", "No existe el pedido.")
                : order;
        }
    }
}
