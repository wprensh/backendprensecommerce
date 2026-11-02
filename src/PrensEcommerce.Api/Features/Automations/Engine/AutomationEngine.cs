using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.MultiTenancy;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Automations.Engine;

/// <summary>Resumen de una pasada del motor para la tienda actual.</summary>
public sealed record AutomationRunResult(int EventsProcessed, int RunsScheduled, int Succeeded, int Skipped, int Failed);

/// <summary>
/// Motor de automatizaciones de UNA tienda (la del <see cref="ITenantProvider"/> del scope).
/// Cada pasada:
/// <list type="number">
/// <item>convierte eventos de cliente en ejecuciones programadas (<see cref="AutomationRun"/>);</item>
/// <item>detecta carritos abandonados y clientes inactivos;</item>
/// <item>ejecuta las ejecuciones vencidas con el <see cref="IAutomationActionHandler"/> de su acción.</item>
/// </list>
/// Es idempotente: cada ejecución lleva una clave única y repetir la pasada no duplica nada.
/// </summary>
public sealed class AutomationEngine(
    AppDbContext db,
    ITenantProvider tenant,
    IEnumerable<IAutomationActionHandler> handlers,
    TimeProvider clock,
    IOptions<AutomationOptions> options,
    ILogger<AutomationEngine> logger)
{
    private readonly AutomationOptions _options = options.Value;
    private readonly Dictionary<AutomationAction, IAutomationActionHandler> _handlers = handlers.ToDictionary(h => h.Action);

    public async Task<AutomationRunResult> RunAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var rules = (await db.AutomationRules.AsNoTracking().Where(r => r.IsActive).ToListAsync(ct))
            .Where(r => AutomationTriggers.IsSupported(r.Trigger))
            .ToList();

        var (eventsProcessed, fromEvents) = await ScheduleFromEventsAsync(rules, now, ct);
        var fromCarts = await ScheduleAbandonedCartsAsync(rules, now, ct);
        var fromInactivity = await ScheduleInactiveCustomersAsync(rules, now, ct);
        var scheduled = await SaveNewRunsAsync([.. fromEvents, .. fromCarts, .. fromInactivity], ct);

        var (succeeded, skipped, failed) = await ExecuteDueRunsAsync(now, ct);
        return new AutomationRunResult(eventsProcessed, scheduled, succeeded, skipped, failed);
    }

    private async Task<(int Processed, List<AutomationRun> Runs)> ScheduleFromEventsAsync(
        List<AutomationRule> rules, DateTimeOffset now, CancellationToken ct)
    {
        var events = await db.CustomerEvents
            .Where(e => e.ProcessedAt == null)
            .OrderBy(e => e.OccurredAt)
            .Take(_options.BatchSize)
            .ToListAsync(ct);

        // Número de pedido legible (PE-000123) para las plantillas.
        var orderIds = events.Where(e => e.ReferenceId != null && e.Type is AutomationTrigger.OrderPlaced or AutomationTrigger.OrderPaid)
            .Select(e => e.ReferenceId!.Value).Distinct().ToList();
        var orderNumbers = orderIds.Count == 0
            ? []
            : await db.Orders.AsNoTracking().Where(o => orderIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.OrderNumber, ct);

        var runs = new List<AutomationRun>();
        foreach (var evt in events)
        {
            evt.ProcessedAt = now;
            var orderNumber = evt.ReferenceId is { } refId && orderNumbers.TryGetValue(refId, out var number) ? number : null;
            foreach (var rule in rules.Where(r => r.Trigger == evt.Type))
            {
                runs.Add(NewRun(rule, evt.UserId, evt.OccurredAt.AddMinutes(rule.DelayMinutes), $"{rule.Id:N}:e:{evt.Id:N}",
                    new JsonObject { ["eventId"] = evt.Id, ["referenceId"] = evt.ReferenceId, ["orderNumber"] = orderNumber, ["amount"] = evt.Amount }));
            }
        }

        return (events.Count, runs);
    }

    private async Task<List<AutomationRun>> ScheduleAbandonedCartsAsync(List<AutomationRule> rules, DateTimeOffset now, CancellationToken ct)
    {
        var runs = new List<AutomationRun>();

        foreach (var rule in rules.Where(r => r.Trigger == AutomationTrigger.CartAbandoned))
        {
            var idleSince = now.AddMinutes(-rule.DelayMinutes);
            var oldest = idleSince.AddDays(-_options.AbandonedCartMaxAgeDays);

            var carts = await db.CartActivities.AsNoTracking()
                .Where(c => c.ItemCount > 0 && c.LastActivityAt <= idleSince && c.LastActivityAt >= oldest)
                .OrderBy(c => c.LastActivityAt)
                .Take(_options.BatchSize)
                .ToListAsync(ct);

            // Una vez por "sesión" de carrito: si el cliente vuelve a tocarlo, cambia la clave.
            runs.AddRange(carts.Select(cart => NewRun(rule, cart.UserId, now,
                $"{rule.Id:N}:cart:{cart.UserId:N}:{cart.LastActivityAt.ToUnixTimeMilliseconds()}",
                new JsonObject { ["units"] = cart.ItemCount, ["cartUpdatedAt"] = cart.LastActivityAt })));
        }

        return runs;
    }

    private async Task<List<AutomationRun>> ScheduleInactiveCustomersAsync(List<AutomationRule> rules, DateTimeOffset now, CancellationToken ct)
    {
        var runs = new List<AutomationRun>();

        foreach (var rule in rules.Where(r => r.Trigger == AutomationTrigger.Inactivity))
        {
            var cutoff = now.AddMinutes(-rule.DelayMinutes);

            var dormant = await db.Orders.AsNoTracking()
                .Where(o => o.Status != OrderStatus.Cancelled)
                .GroupBy(o => o.UserId)
                .Select(g => new { UserId = g.Key, LastOrderAt = g.Max(o => o.CreatedAt) })
                .Where(x => x.LastOrderAt <= cutoff)
                .OrderBy(x => x.LastOrderAt)
                .Take(_options.BatchSize)
                .ToListAsync(ct);

            // Una vez por cada "último pedido": si vuelve a comprar y se vuelve a dormir, se repite.
            runs.AddRange(dormant.Select(d => NewRun(rule, d.UserId, now,
                $"{rule.Id:N}:inactive:{d.UserId:N}:{d.LastOrderAt.ToUnixTimeSeconds()}",
                new JsonObject { ["lastOrderAt"] = d.LastOrderAt })));
        }

        return runs;
    }

    /// <summary>Inserta solo las ejecuciones cuya clave de idempotencia aún no existe.</summary>
    private async Task<int> SaveNewRunsAsync(List<AutomationRun> candidates, CancellationToken ct)
    {
        var fresh = candidates.DistinctBy(r => r.DedupKey).ToList();
        if (fresh.Count > 0)
        {
            var keys = fresh.Select(r => r.DedupKey).ToList();
            var existing = (await db.AutomationRuns.Where(r => keys.Contains(r.DedupKey)).Select(r => r.DedupKey!).ToListAsync(ct))
                .ToHashSet(StringComparer.Ordinal);
            fresh.RemoveAll(r => existing.Contains(r.DedupKey!));
            db.AutomationRuns.AddRange(fresh);
        }

        await db.SaveChangesAsync(ct); // también persiste ProcessedAt de los eventos
        return fresh.Count;
    }

    private async Task<(int Succeeded, int Skipped, int Failed)> ExecuteDueRunsAsync(DateTimeOffset now, CancellationToken ct)
    {
        var due = await db.AutomationRuns
            .Where(r => r.Status == AutomationRunStatus.Pending && r.ScheduledAt <= now)
            .OrderBy(r => r.ScheduledAt)
            .Take(_options.BatchSize)
            .ToListAsync(ct);

        if (due.Count == 0)
        {
            return (0, 0, 0);
        }

        var ruleIds = due.Select(r => r.RuleId).Distinct().ToList();
        var rules = await db.AutomationRules.AsNoTracking().Where(r => ruleIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);
        var userIds = due.Where(r => r.UserId != null).Select(r => r.UserId!.Value).Distinct().ToList();
        var users = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
        var storeName = tenant.Current?.Name ?? string.Empty;

        int succeeded = 0, skipped = 0, failed = 0;
        foreach (var run in due)
        {
            var payload = JsonNode.Parse(run.Payload) as JsonObject ?? [];
            var outcome = await ExecuteAsync(run, payload, rules, users, storeName, ct);

            run.Status = outcome.Status;
            run.Error = outcome.Error;
            run.ExecutedAt = now;
            if (outcome.Result is not null)
            {
                payload["result"] = outcome.Result;
            }

            run.Payload = payload.ToJsonString();

            _ = outcome.Status switch
            {
                AutomationRunStatus.Succeeded => succeeded++,
                AutomationRunStatus.Skipped => skipped++,
                _ => failed++,
            };
        }

        await db.SaveChangesAsync(ct);
        return (succeeded, skipped, failed);
    }

    private async Task<ActionOutcome> ExecuteAsync(
        AutomationRun run, JsonObject payload, Dictionary<Guid, AutomationRule> rules, Dictionary<Guid, User> users,
        string storeName, CancellationToken ct)
    {
        // La regla pudo desactivarse o borrarse mientras la ejecución esperaba su turno.
        if (!rules.TryGetValue(run.RuleId, out var rule) || !rule.IsActive)
        {
            return ActionOutcome.Skipped("La regla está desactivada.");
        }

        if (run.UserId is not { } userId || !users.TryGetValue(userId, out var user))
        {
            return ActionOutcome.Skipped("El cliente ya no existe.");
        }

        if (rule.Action == AutomationAction.AwardPoints)
        {
            return ActionOutcome.Skipped("Los puntos los asigna el programa de fidelización al pagar el pedido.");
        }

        if (!_handlers.TryGetValue(rule.Action, out var handler))
        {
            return ActionOutcome.Skipped($"La acción {rule.Action} todavía no está disponible.");
        }

        try
        {
            return await handler.ExecuteAsync(new AutomationContext(rule, user, payload, storeName), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Fallo en la ejecución {RunId} de la regla {RuleId}", run.Id, rule.Id);
            return ActionOutcome.Failed(ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message);
        }
    }

    private static AutomationRun NewRun(AutomationRule rule, Guid userId, DateTimeOffset scheduledAt, string dedupKey, JsonObject payload)
    {
        payload["trigger"] = rule.Trigger.ToString();
        return new AutomationRun
        {
            RuleId = rule.Id,
            UserId = userId,
            ScheduledAt = scheduledAt,
            DedupKey = dedupKey,
            Payload = payload.ToJsonString(),
        };
    }
}
