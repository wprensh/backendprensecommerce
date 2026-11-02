using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Features.Automations.Engine;
using PrensEcommerce.Api.Features.Crm;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Automations.Runs;

/// <summary>GET /api/automations/runs?status=&amp;ruleId=&amp;page=&amp;pageSize= — historial de ejecuciones (incluye correos simulados).</summary>
public static class GetRuns
{
    public sealed record Query(AutomationRunStatus? Status, Guid? RuleId, int Page = 1, int PageSize = 20);

    public sealed record Item(
        Guid Id, Guid RuleId, string RuleName, AutomationAction Action, AutomationTrigger Trigger, Guid? UserId, string? CustomerName,
        string? CustomerEmail, AutomationRunStatus Status, DateTimeOffset ScheduledAt, DateTimeOffset? ExecutedAt, string? Error, string Payload);

    public sealed record Response(IReadOnlyList<Item> Items, int Page, int PageSize, int TotalCount);

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/automations/runs", async ([AsParameters] Query query, Handler handler, CancellationToken ct) =>
                    TypedResults.Ok(await handler.HandleAsync(query, ct)))
                .RequireCrmRead()
                .WithTags("Automations")
                .WithName("GetAutomationRuns");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Response> HandleAsync(Query query, CancellationToken ct)
        {
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            var runs = db.AutomationRuns.AsNoTracking();
            if (query.Status is { } status)
            {
                runs = runs.Where(r => r.Status == status);
            }

            if (query.RuleId is { } ruleId)
            {
                runs = runs.Where(r => r.RuleId == ruleId);
            }

            var rows =
                from r in runs
                join rule in db.AutomationRules on r.RuleId equals rule.Id
                join u in db.Users on r.UserId equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby r.ScheduledAt descending
                select new Item(r.Id, r.RuleId, rule.Name, rule.Action, rule.Trigger, r.UserId,
                    u == null ? null : u.Name, u == null ? null : u.Email, r.Status, r.ScheduledAt, r.ExecutedAt, r.Error, r.Payload);

            var total = await runs.CountAsync(ct);
            var items = await rows.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            return new Response(items, page, pageSize, total);
        }
    }
}

/// <summary>
/// POST /api/automations/run — ejecuta ahora una pasada del motor para la tienda actual
/// (útil para probar reglas sin esperar al proceso en segundo plano). Siempre en modo simulación.
/// </summary>
public static class RunNow
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("/automations/run", async (AutomationEngine engine, CancellationToken ct) => TypedResults.Ok(await engine.RunAsync(ct)))
                .RequireCrmWrite()
                .WithTags("Automations")
                .WithName("RunAutomationsNow");
    }
}
