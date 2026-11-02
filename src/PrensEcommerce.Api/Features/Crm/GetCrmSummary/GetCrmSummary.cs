using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.MultiTenancy;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Crm.GetCrmSummary;

/// <summary>GET /api/crm/summary — indicadores para la cabecera del CRM.</summary>
public static class GetCrmSummary
{
    public sealed record TagCount(string Tag, int Customers);

    public sealed record Response(
        int Customers, int NewLast30Days, int Buyers, int RepeatBuyers, int AcceptsMarketing, int ActiveCarts,
        decimal Revenue, decimal AverageOrderValue, int AutomationsLast7Days, IReadOnlyList<TagCount> TopTags);

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/crm/summary", async (Handler handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(ct)))
                .RequireCrmRead()
                .WithTags("CRM")
                .WithName("GetCrmSummary");
    }

    public sealed class Handler(AppDbContext db, ITenantProvider tenant, TimeProvider clock) : IHandler
    {
        public async Task<Response> HandleAsync(CancellationToken ct)
        {
            var now = clock.GetUtcNow();
            var monthAgo = now.AddDays(-30);
            var weekAgo = now.AddDays(-7);
            var customers = CustomerQueries.Customers(db);

            // Consultas agregadas sencillas: EF no traduce un GroupBy sobre la proyección con subconsultas.
            var total = await customers.CountAsync(ct);
            var newCustomers = await customers.CountAsync(c => c.RegisteredAt >= monthAgo, ct);
            var buyers = await customers.CountAsync(c => c.OrderCount > 0, ct);
            var repeat = await customers.CountAsync(c => c.OrderCount > 1, ct);
            var marketing = await customers.CountAsync(c => c.AcceptsMarketing, ct);
            var carts = await customers.CountAsync(c => c.CartItemCount > 0, ct);
            var paidOrders = db.Orders.Where(o => o.Status != OrderStatus.Cancelled);
            var revenue = await paidOrders.SumAsync(o => (decimal?)o.Total, ct) ?? 0m;
            var orders = await paidOrders.CountAsync(ct);

            var automations = await db.AutomationRuns.CountAsync(r => r.ExecutedAt >= weekAgo, ct);

            // unnest() de text[] no es traducible desde LINQ: SQL directo, con el tenant explícito.
            var tags = await db.Database.SqlQuery<TagCount>($"""
                    SELECT t AS tag, count(*)::int AS customers
                    FROM identity.users u, unnest(u.tags) AS t
                    WHERE u.tenant_id = {tenant.TenantId}
                    GROUP BY t
                    ORDER BY customers DESC, t
                    LIMIT 10
                    """)
                .ToListAsync(ct);

            var average = orders == 0 ? 0m : Math.Round(revenue / orders, 2);
            return new Response(total, newCustomers, buyers, repeat, marketing, carts, revenue, average, automations, tags);
        }
    }
}
