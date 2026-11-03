using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Authorization;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Orders.Admin.GetOrders;

/// <summary>
/// GET /api/admin/orders?search=&amp;status=&amp;from=&amp;to=&amp;page=&amp;pageSize= — pedidos de la tienda
/// con recuento por estado (para las pestañas del listado).
/// </summary>
public static class GetOrders
{
    /// <param name="Status">Estado en texto, sin distinguir mayúsculas ("pending", "Paid"…), igual que en el JSON.</param>
    public sealed record Query(string? Search, string? Status, DateOnly? From, DateOnly? To, int Page = 1, int PageSize = 20);

    public sealed record Response(
        IReadOnlyList<AdminOrderSummary> Items,
        int Page,
        int PageSize,
        int TotalCount,
        IReadOnlyDictionary<OrderStatus, int> CountByStatus);

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet(OrderWorkflow.BasePath, async ([AsParameters] Query query, Handler handler, CancellationToken ct) =>
                    TypedResults.Ok(await handler.HandleAsync(query, ct)))
                .RequirePermission(Permissions.OrdersRead)
                .WithTags(OrderWorkflow.Tag)
                .WithName("AdminGetOrders");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Response> HandleAsync(Query query, CancellationToken ct)
        {
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            // Filtros comunes (búsqueda y fechas); el de estado se aplica después para poder contar por estado.
            var orders =
                from o in db.Orders.AsNoTracking()
                join u in db.Users.AsNoTracking() on o.UserId equals u.Id
                select new { Order = o, User = u };

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var pattern = $"%{query.Search.Trim()}%";
                orders = orders.Where(x =>
                    EF.Functions.ILike(x.Order.OrderNumber, pattern) ||
                    EF.Functions.ILike(x.User.Name, pattern) ||
                    EF.Functions.ILike(x.User.Email, pattern));
            }

            if (query.From is { } from)
            {
                var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                orders = orders.Where(x => x.Order.CreatedAt >= start);
            }

            if (query.To is { } to)
            {
                var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                orders = orders.Where(x => x.Order.CreatedAt < end);
            }

            var counts = await orders
                .GroupBy(x => x.Order.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Status, g => g.Count, ct);

            if (Enum.TryParse<OrderStatus>(query.Status, ignoreCase: true, out var status))
            {
                orders = orders.Where(x => x.Order.Status == status);
            }

            var total = await orders.CountAsync(ct);
            var items = await orders
                .OrderByDescending(x => x.Order.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new AdminOrderSummary(
                    x.Order.Id,
                    x.Order.OrderNumber,
                    x.Order.Status,
                    x.Order.PaymentMethod,
                    new OrderCustomer(x.User.Id, x.User.Name, x.User.Email),
                    x.Order.Items.Sum(i => i.Quantity),
                    x.Order.Total,
                    x.Order.Currency,
                    x.Order.CreatedAt))
                .ToListAsync(ct);

            var countByStatus = Enum.GetValues<OrderStatus>().ToDictionary(s => s, s => counts.GetValueOrDefault(s));
            return new Response(items, page, pageSize, total, countByStatus);
        }
    }
}
