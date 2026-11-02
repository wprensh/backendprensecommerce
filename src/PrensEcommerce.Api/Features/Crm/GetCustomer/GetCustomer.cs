using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Crm.GetCustomer;

/// <summary>GET /api/crm/customers/{id} — ficha 360º: métricas, pedidos, notas, actividad y automatizaciones.</summary>
public static class GetCustomer
{
    public sealed record OrderSummary(Guid Id, DateTimeOffset CreatedAt, OrderStatus Status, decimal Total, int Units);

    public sealed record Note(Guid Id, string Body, string? AuthorName, DateTimeOffset CreatedAt);

    public sealed record Activity(AutomationTrigger Type, Guid? ReferenceId, decimal? Amount, DateTimeOffset OccurredAt);

    public sealed record AutomationHistory(
        Guid Id, string RuleName, AutomationAction Action, AutomationRunStatus Status, DateTimeOffset ScheduledAt,
        DateTimeOffset? ExecutedAt, string? Error, string Payload);

    public sealed record Response(
        Guid Id, string Name, string Email, string? Phone, bool IsActive, bool AcceptsMarketing, IReadOnlyList<string> Tags,
        DateTimeOffset RegisteredAt, int OrderCount, decimal TotalSpent, decimal AverageOrderValue, DateTimeOffset? LastOrderAt,
        int CartItemCount, DateTimeOffset? CartUpdatedAt,
        IReadOnlyList<OrderSummary> Orders, IReadOnlyList<Note> Notes, IReadOnlyList<Activity> Activity,
        IReadOnlyList<AutomationHistory> Automations);

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/crm/customers/{id:guid}", async (Guid id, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(id, ct)).Match(TypedResults.Ok))
                .RequireCrmRead()
                .WithTags("CRM")
                .WithName("GetCrmCustomer");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        private const int HistoryLimit = 20;

        public async Task<Result<Response>> HandleAsync(Guid id, CancellationToken ct)
        {
            var c = await CustomerQueries.Customers(db).FirstOrDefaultAsync(x => x.Id == id, ct);
            if (c is null)
            {
                return Error.NotFound("Customer.NotFound", "El cliente no existe en esta tienda.");
            }

            var orders = await db.Orders.AsNoTracking()
                .Where(o => o.UserId == id)
                .OrderByDescending(o => o.CreatedAt)
                .Take(HistoryLimit)
                .Select(o => new OrderSummary(o.Id, o.CreatedAt, o.Status, o.Total, o.Items.Sum(i => i.Quantity)))
                .ToListAsync(ct);

            var notes = await (
                    from n in db.CustomerNotes.AsNoTracking()
                    where n.CustomerId == id
                    join a in db.Users on n.AuthorId equals a.Id into authors
                    from a in authors.DefaultIfEmpty()
                    orderby n.CreatedAt descending
                    select new Note(n.Id, n.Body, a == null ? null : a.Name, n.CreatedAt))
                .ToListAsync(ct);

            var activity = await db.CustomerEvents.AsNoTracking()
                .Where(e => e.UserId == id)
                .OrderByDescending(e => e.OccurredAt)
                .Take(HistoryLimit)
                .Select(e => new Activity(e.Type, e.ReferenceId, e.Amount, e.OccurredAt))
                .ToListAsync(ct);

            var automations = await (
                    from r in db.AutomationRuns.AsNoTracking()
                    where r.UserId == id
                    join rule in db.AutomationRules on r.RuleId equals rule.Id
                    orderby r.ScheduledAt descending
                    select new AutomationHistory(r.Id, rule.Name, rule.Action, r.Status, r.ScheduledAt, r.ExecutedAt, r.Error, r.Payload))
                .Take(HistoryLimit)
                .ToListAsync(ct);

            var average = c.OrderCount == 0 ? 0m : Math.Round(c.TotalSpent / c.OrderCount, 2);

            return new Response(
                c.Id, c.Name, c.Email, c.Phone, c.IsActive, c.AcceptsMarketing, c.Tags, c.RegisteredAt,
                c.OrderCount, c.TotalSpent, average, c.LastOrderAt, c.CartItemCount, c.CartUpdatedAt,
                orders, notes, activity, automations);
        }
    }
}
