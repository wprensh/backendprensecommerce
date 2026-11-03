using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Authorization;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Dashboard;

/// <summary>
/// GET /api/admin/dashboard?days=30 — resumen de la tienda para la portada de la administración:
/// ventas del periodo frente al anterior, serie diaria, pedidos por estado, productos más vendidos,
/// stock bajo y tareas pendientes. Las fechas se agrupan en hora de Colombia.
/// </summary>
public static class GetDashboard
{
    public const int LowStockThreshold = 5;

    public sealed record Query(int Days = 30);

    public sealed record Kpi(decimal Value, decimal Previous);

    public sealed record DailySales(DateOnly Date, decimal Revenue, int Orders);

    public sealed record TopProduct(Guid ProductId, string Name, int Quantity, decimal Revenue);

    public sealed record LowStockProduct(Guid Id, string Name, string? Sku, int Stock);

    public sealed record RecentOrder(Guid Id, string OrderNumber, string CustomerName, OrderStatus Status, decimal Total, DateTimeOffset CreatedAt);

    public sealed record Pending(int OrdersToConfirm, int OrdersToShip, int OpenTickets, int HiddenReviews);

    public sealed record Response(
        int Days,
        DateOnly From,
        DateOnly To,
        Kpi Revenue,
        Kpi Orders,
        Kpi AverageTicket,
        Kpi NewCustomers,
        IReadOnlyList<DailySales> Sales,
        IReadOnlyDictionary<OrderStatus, int> OrdersByStatus,
        IReadOnlyList<TopProduct> TopProducts,
        IReadOnlyList<LowStockProduct> LowStock,
        IReadOnlyList<RecentOrder> RecentOrders,
        Pending Pending);

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/admin/dashboard", async ([AsParameters] Query query, Handler handler, CancellationToken ct) =>
                    TypedResults.Ok(await handler.HandleAsync(query, ct)))
                .RequirePermission(Permissions.AnalyticsRead)
                .WithTags("Admin · Dashboard")
                .WithName("AdminGetDashboard");
    }

    public sealed class Handler(AppDbContext db, TimeProvider clock) : IHandler
    {
        private static readonly TimeZoneInfo StoreZone = ResolveZone();

        private static readonly OrderStatus[] SoldStatuses = [OrderStatus.Paid, OrderStatus.Shipped, OrderStatus.Delivered];

        public async Task<Response> HandleAsync(Query query, CancellationToken ct)
        {
            var days = Math.Clamp(query.Days, 1, 365);
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), StoreZone).DateTime);
            var from = today.AddDays(1 - days);
            var start = StartOf(from);
            var end = StartOf(today.AddDays(1));
            var previousStart = StartOf(from.AddDays(-days));

            // Pedidos vendidos de ambos periodos: pocos campos, se agrupan en memoria por día local.
            var sold = await db.Orders.AsNoTracking()
                .Where(o => SoldStatuses.Contains(o.Status) && o.CreatedAt >= previousStart && o.CreatedAt < end)
                .Select(o => new { o.CreatedAt, o.Total })
                .ToListAsync(ct);
            var current = sold.Where(o => o.CreatedAt >= start).ToList();
            var previous = sold.Where(o => o.CreatedAt < start).ToList();

            var revenue = current.Sum(o => o.Total);
            var previousRevenue = previous.Sum(o => o.Total);

            var byDay = current
                .GroupBy(o => LocalDate(o.CreatedAt))
                .ToDictionary(g => g.Key, g => (Revenue: g.Sum(o => o.Total), Orders: g.Count()));
            var series = Enumerable.Range(0, days)
                .Select(i => from.AddDays(i))
                .Select(d => byDay.TryGetValue(d, out var v) ? new DailySales(d, v.Revenue, v.Orders) : new DailySales(d, 0, 0))
                .ToList();

            var customerRoleId = await db.Roles.AsNoTracking()
                .Where(r => r.Name == SystemRoles.Customer).Select(r => (Guid?)r.Id).FirstOrDefaultAsync(ct);
            var signups = await db.Users.AsNoTracking()
                .Where(u => u.CreatedAt >= previousStart && u.CreatedAt < end && u.Roles.Any(r => r.RoleId == customerRoleId))
                .Select(u => u.CreatedAt)
                .ToListAsync(ct);

            var byStatus = await db.Orders.AsNoTracking()
                .Where(o => o.CreatedAt >= start && o.CreatedAt < end)
                .GroupBy(o => o.Status)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Count, ct);
            var ordersByStatus = Enum.GetValues<OrderStatus>().ToDictionary(s => s, s => byStatus.GetValueOrDefault(s));

            var topProducts = await db.Orders.AsNoTracking()
                .Where(o => SoldStatuses.Contains(o.Status) && o.CreatedAt >= start && o.CreatedAt < end)
                .SelectMany(o => o.Items)
                .GroupBy(i => new { i.ProductId, i.ProductName })
                .Select(g => new { g.Key.ProductId, g.Key.ProductName, Quantity = g.Sum(i => i.Quantity), Revenue = g.Sum(i => i.UnitPrice * i.Quantity) })
                .OrderByDescending(t => t.Quantity).ThenByDescending(t => t.Revenue)
                .Take(5)
                .ToListAsync(ct);

            var lowStock = await db.Products.AsNoTracking()
                .Where(p => p.IsActive && p.Stock <= LowStockThreshold)
                .OrderBy(p => p.Stock).ThenBy(p => p.Name)
                .Take(8)
                .Select(p => new LowStockProduct(p.Id, p.Name, p.Sku, p.Stock))
                .ToListAsync(ct);

            var recent = await (
                    from o in db.Orders.AsNoTracking()
                    join u in db.Users.AsNoTracking() on o.UserId equals u.Id
                    orderby o.CreatedAt descending
                    select new RecentOrder(o.Id, o.OrderNumber, u.Name, o.Status, o.Total, o.CreatedAt))
                .Take(6)
                .ToListAsync(ct);

            var pending = new Pending(
                await db.Orders.CountAsync(o => o.Status == OrderStatus.Pending, ct),
                await db.Orders.CountAsync(o => o.Status == OrderStatus.Paid, ct),
                await db.SupportTickets.CountAsync(t => t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress, ct),
                await db.ProductReviews.CountAsync(r => r.IsHidden, ct));

            return new Response(
                days, from, today,
                new Kpi(revenue, previousRevenue),
                new Kpi(current.Count, previous.Count),
                new Kpi(Average(revenue, current.Count), Average(previousRevenue, previous.Count)),
                new Kpi(signups.Count(d => d >= start), signups.Count(d => d < start)),
                series, ordersByStatus,
                topProducts.Select(t => new TopProduct(t.ProductId, t.ProductName, t.Quantity, t.Revenue)).ToList(),
                lowStock, recent, pending);
        }

        private static decimal Average(decimal total, int count) =>
            count == 0 ? 0 : Math.Round(total / count, 0, MidpointRounding.AwayFromZero);

        private static DateOnly LocalDate(DateTimeOffset instant) =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, StoreZone).DateTime);

        private static DateTimeOffset StartOf(DateOnly date)
        {
            var local = date.ToDateTime(TimeOnly.MinValue);
            return new DateTimeOffset(local, StoreZone.GetUtcOffset(local)).ToUniversalTime();
        }

        private static TimeZoneInfo ResolveZone()
        {
            // La tienda opera en COP; Colombia no tiene horario de verano (UTC−5 todo el año).
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.CreateCustomTimeZone("America/Bogota", TimeSpan.FromHours(-5), "Colombia", "Colombia");
            }
        }
    }
}
