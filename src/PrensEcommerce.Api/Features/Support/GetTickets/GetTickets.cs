using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Support.GetTickets;

/// <summary>GET /api/admin/support/tickets — bandeja del personal con filtros, paginada.</summary>
public static class GetTickets
{
    /// <summary>Estado y prioridad como texto, sin distinguir mayúsculas ("open", "waitingCustomer"…).</summary>
    public sealed record Query(string? Status, string? Priority, string? Search, int Page = 1, int PageSize = 20);

    public sealed record Counters(int Open, int InProgress, int WaitingCustomer, int Resolved, int Closed, double? AverageRating);

    public sealed record Response(IReadOnlyList<TicketSummary> Items, int Total, int Page, int PageSize, Counters Counters);

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/admin/support/tickets", async ([AsParameters] Query query, Handler handler, CancellationToken ct) =>
                    TypedResults.Ok(await handler.HandleAsync(query, ct)))
                .RequireAuthorization(AppPolicies.SupportAgent)
                .WithTags("Support · Admin")
                .WithName("GetTickets");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Response> HandleAsync(Query query, CancellationToken ct)
        {
            var page = Math.Max(query.Page, 1);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            var tickets = db.SupportTickets.AsNoTracking();
            if (Enum.TryParse<TicketStatus>(query.Status, ignoreCase: true, out var status))
            {
                tickets = tickets.Where(t => t.Status == status);
            }

            if (Enum.TryParse<TicketPriority>(query.Priority, ignoreCase: true, out var priority))
            {
                tickets = tickets.Where(t => t.Priority == priority);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var term = query.Search.Trim();
                var number = int.TryParse(term.Replace("TK-", "", StringComparison.OrdinalIgnoreCase), out var n) ? n : -1;
                tickets = tickets.Where(t => t.Number == number || EF.Functions.ILike(t.Subject, $"%{term}%"));
            }

            var total = await tickets.CountAsync(ct);
            var items = await TicketQueries.ToSummaries(tickets
                    .OrderByDescending(t => t.Priority)
                    .ThenBy(t => t.Status)
                    .ThenByDescending(t => t.UpdatedAt ?? t.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize), includeInternal: true)
                .ToListAsync(ct);

            var byStatus = await db.SupportTickets.AsNoTracking()
                .GroupBy(t => t.Status)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
            var averageRating = await db.SupportTickets.AsNoTracking()
                .Where(t => t.SatisfactionScore != null)
                .AverageAsync(t => (double?)t.SatisfactionScore, ct);

            var counters = new Counters(
                byStatus.GetValueOrDefault(TicketStatus.Open),
                byStatus.GetValueOrDefault(TicketStatus.InProgress),
                byStatus.GetValueOrDefault(TicketStatus.WaitingCustomer),
                byStatus.GetValueOrDefault(TicketStatus.Resolved),
                byStatus.GetValueOrDefault(TicketStatus.Closed),
                averageRating is null ? null : Math.Round(averageRating.Value, 2));

            return new Response(items, total, page, pageSize, counters);
        }
    }
}
