using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Authorization;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Orders.Admin.GetOrder;

/// <summary>GET /api/admin/orders/{id} — detalle completo para el equipo, con los estados a los que puede pasar.</summary>
public static class GetOrder
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet($"{OrderWorkflow.BasePath}/{{id:guid}}", async (Guid id, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(id, ct)).Match(TypedResults.Ok))
                .RequirePermission(Permissions.OrdersRead)
                .WithTags(OrderWorkflow.Tag)
                .WithName("AdminGetOrder");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<AdminOrderDetail>> HandleAsync(Guid id, CancellationToken ct)
        {
            var order = await db.Orders.AsNoTracking()
                .Where(o => o.Id == id)
                .Select(OrderResponse.Projection)
                .FirstOrDefaultAsync(ct);
            if (order is null)
            {
                return Error.NotFound("Orders.NotFound", "El pedido no existe.");
            }

            var extra = await (
                    from o in db.Orders.AsNoTracking()
                    join u in db.Users.AsNoTracking() on o.UserId equals u.Id
                    where o.Id == id
                    select new { Customer = new OrderCustomer(u.Id, u.Name, u.Email), o.LoyaltyDiscount, o.UpdatedAt })
                .FirstAsync(ct);

            return new AdminOrderDetail(order, extra.Customer, extra.LoyaltyDiscount, extra.UpdatedAt,
                OrderWorkflow.Next(order.Status));
        }
    }
}
