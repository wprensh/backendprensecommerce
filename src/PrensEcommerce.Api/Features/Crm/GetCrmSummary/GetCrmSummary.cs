using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Domain;
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

    public sealed class Handler(AppDbContext db, TimeProvider clock) : IHandler
    {
        public async Task<Response> HandleAsync(CancellationToken ct)
        {
            var now = clock.GetUtcNow();
            var monthAgo = now.AddDays(-30);
            var weekAgo = now.AddDays(-7);
            var customers = CustomerQueries.Customers(db);

            var stats = await customers
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    New = g.Count(c => c.RegisteredAt >= monthAgo),
                    Buyers = g.Count(c => c.OrderCount > 0),
                    Repeat = g.Count(c => c.OrderCount > 1),
                    Marketing = g.Count(c => c.AcceptsMarketing),
                    Carts = g.Count(c => c.CartItemCount > 0),
                    Revenue = g.Sum(c => c.TotalSpent),
                    Orders = g.Sum(c => c.OrderCount),
                })
                .FirstOrDefaultAsync(ct);

            var automations = await db.AutomationRuns.CountAsync(r => r.ExecutedAt >= weekAgo, ct);

            var tags = await customers
                .SelectMany(c => c.Tags)
                .GroupBy(t => t)
                .Select(g => new TagCount(g.Key, g.Count()))
                .OrderByDescending(t => t.Customers)
                .Take(10)
                .ToListAsync(ct);

            if (stats is null)
            {
                return new Response(0, 0, 0, 0, 0, 0, 0m, 0m, automations, tags);
            }

            var average = stats.Orders == 0 ? 0m : Math.Round(stats.Revenue / stats.Orders, 2);
            return new Response(stats.Total, stats.New, stats.Buyers, stats.Repeat, stats.Marketing, stats.Carts,
                stats.Revenue, average, automations, tags);
        }
    }
}
