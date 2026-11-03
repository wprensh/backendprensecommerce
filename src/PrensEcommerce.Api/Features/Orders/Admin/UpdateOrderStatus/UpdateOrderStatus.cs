using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Authorization;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Features.Loyalty;
using GetOrderSlice = PrensEcommerce.Api.Features.Orders.Admin.GetOrder.GetOrder;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Orders.Admin.UpdateOrderStatus;

/// <summary>
/// PATCH /api/admin/orders/{id}/status — avanza el pedido según <see cref="OrderWorkflow"/>.
/// Marcar como pagado confirma el cobro pendiente (transferencia o efectivo).
/// Cancelar devuelve el stock y retira los puntos que ganó el pedido.
/// </summary>
public static class UpdateOrderStatus
{
    public sealed record Command(OrderStatus Status);

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPatch($"{OrderWorkflow.BasePath}/{{id:guid}}/status",
                    async (Guid id, Command command, Handler handler, CancellationToken ct) =>
                        (await handler.HandleAsync(id, command, ct)).Match(TypedResults.Ok))
                .RequirePermission(Permissions.OrdersWrite)
                .WithTags(OrderWorkflow.Tag)
                .WithName("AdminUpdateOrderStatus");
    }

    public sealed class Handler(AppDbContext db, LoyaltyService loyalty, TimeProvider clock, GetOrderSlice.Handler details) : IHandler
    {
        public async Task<Result<AdminOrderDetail>> HandleAsync(Guid id, Command command, CancellationToken ct)
        {
            var order = await db.Orders
                .Include(o => o.Items)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.Id == id, ct);
            if (order is null)
            {
                return Error.NotFound("Orders.NotFound", "El pedido no existe.");
            }

            if (!OrderWorkflow.CanMove(order.Status, command.Status))
            {
                return Error.Validation("Orders.InvalidTransition",
                    $"Un pedido {OrderWorkflow.Label(order.Status).ToLowerInvariant()} no puede pasar a {OrderWorkflow.Label(command.Status).ToLowerInvariant()}.");
            }

            switch (command.Status)
            {
                case OrderStatus.Paid:
                    ConfirmPendingPayment(order);
                    break;
                case OrderStatus.Cancelled:
                    await RestockAsync(order, ct);
                    await RevertPointsAsync(order, ct);
                    break;
            }

            order.Status = command.Status;
            await db.SaveChangesAsync(ct);

            return await details.HandleAsync(id, ct);
        }

        private void ConfirmPendingPayment(Order order)
        {
            var payment = order.Payments.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
            if (payment is { Status: PaymentStatus.Pending or PaymentStatus.Authorized })
            {
                payment.Status = PaymentStatus.Captured;
                payment.ProcessedAt = clock.GetUtcNow();
            }
        }

        private async Task RestockAsync(Order order, CancellationToken ct)
        {
            var productIds = order.Items.Select(i => i.ProductId).ToList();
            var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
            foreach (var item in order.Items)
            {
                if (products.TryGetValue(item.ProductId, out var product))
                {
                    product.Stock += item.Quantity;
                }
            }
        }

        private async Task RevertPointsAsync(Order order, CancellationToken ct)
        {
            var earned = await db.LoyaltyTransactions
                .Where(t => t.OrderId == order.Id && t.Type == LoyaltyTransactionType.Earn)
                .SumAsync(t => t.Points, ct);
            if (earned <= 0)
            {
                return;
            }

            var account = await loyalty.GetOrCreateAccountAsync(order.UserId, ct);
            // Si ya canjeó parte de esos puntos, solo se retira lo que queda en el saldo.
            var toRemove = Math.Min(earned, account.PointsBalance);
            if (toRemove > 0)
            {
                loyalty.Adjust(account, -toRemove, $"Pedido {order.OrderNumber} cancelado");
            }
        }
    }
}
