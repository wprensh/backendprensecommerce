using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Orders.GetMyOrders;

/// <summary>GET /api/orders — historial de pedidos del usuario, más recientes primero.</summary>
public static class GetMyOrders
{
    public sealed record Item(
        Guid Id,
        string OrderNumber,
        OrderStatus Status,
        PaymentMethod PaymentMethod,
        int ItemCount,
        decimal Total,
        string Currency,
        DateTimeOffset CreatedAt);

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/orders", async (ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                    TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), ct)))
                .RequireAuthorization()
                .WithTags("Orders")
                .WithName("GetMyOrders");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        private const int MaxOrders = 50;

        public async Task<IReadOnlyList<Item>> HandleAsync(Guid userId, CancellationToken ct) =>
            await db.Orders.AsNoTracking()
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .Take(MaxOrders)
                .Select(o => new Item(
                    o.Id, o.OrderNumber, o.Status, o.PaymentMethod,
                    o.Items.Sum(i => i.Quantity), o.Total, o.Currency, o.CreatedAt))
                .ToListAsync(ct);
    }
}
