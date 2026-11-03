using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Features.Automations.Engine;
using PrensEcommerce.Api.Features.Crm;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Automations.Rules;

public sealed record RuleResponse(
    Guid Id, string Name, AutomationTrigger Trigger, AutomationAction Action, JsonElement Configuration,
    int DelayMinutes, bool IsActive, bool Supported, int Executions, DateTimeOffset? LastExecutedAt);

/// <summary>Alta y edición de reglas. <see cref="Configuration"/> es un objeto JSON libre según la acción.</summary>
public sealed record RuleCommand(string Name, AutomationTrigger Trigger, AutomationAction Action, JsonElement Configuration, int DelayMinutes, bool IsActive);

public sealed class RuleCommandValidator : AbstractValidator<RuleCommand>
{
    public RuleCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Trigger).IsInEnum();
        RuleFor(c => c.Action).IsInEnum();
        RuleFor(c => c.DelayMinutes).InclusiveBetween(0, 525_600); // hasta 1 año
        RuleFor(c => c.Configuration.ValueKind).Equal(JsonValueKind.Object).WithMessage("La configuración debe ser un objeto JSON.");
        RuleFor(c => c).Must(HasEmailContent).When(c => c.Action == AutomationAction.SendEmail && c.Configuration.ValueKind == JsonValueKind.Object)
            .WithMessage("Para enviar correo indica una plantilla (template) o un asunto y cuerpo (subject, body).");
        RuleFor(c => c).Must(c => GetString(c.Configuration, "tag") is { Length: > 0 and <= 40 })
            .When(c => c.Action == AutomationAction.AddTag && c.Configuration.ValueKind == JsonValueKind.Object)
            .WithMessage("Para etiquetar indica la etiqueta (tag), de hasta 40 caracteres.");
    }

    private static bool HasEmailContent(RuleCommand c) =>
        DefaultEmailTemplates.Find(GetString(c.Configuration, "template")) is not null ||
        (!string.IsNullOrWhiteSpace(GetString(c.Configuration, "subject")) && !string.IsNullOrWhiteSpace(GetString(c.Configuration, "body")));

    private static string? GetString(JsonElement json, string property) =>
        json.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

/// <summary>GET /api/automations/rules — reglas con sus estadísticas de ejecución.</summary>
public static class GetRules
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/automations/rules", async (Handler handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(ct)))
                .RequireCrmRead()
                .WithTags("Automations")
                .WithName("GetAutomationRules");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<IReadOnlyList<RuleResponse>> HandleAsync(CancellationToken ct)
        {
            var rows = await db.AutomationRules.AsNoTracking()
                .OrderBy(r => r.Name)
                .Select(r => new
                {
                    Rule = r,
                    Executions = db.AutomationRuns.Count(x => x.RuleId == r.Id && x.ExecutedAt != null),
                    LastExecutedAt = db.AutomationRuns.Where(x => x.RuleId == r.Id).Max(x => x.ExecutedAt),
                })
                .ToListAsync(ct);

            return [.. rows.Select(x => RuleMapping.ToResponse(x.Rule, x.Executions, x.LastExecutedAt))];
        }
    }
}

/// <summary>GET /api/automations/templates — plantillas de correo incluidas y variables disponibles.</summary>
public static class GetTemplates
{
    public sealed record Response(IReadOnlyList<DefaultEmailTemplates.Template> Templates, IReadOnlyList<string> Variables);

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/automations/templates", () => TypedResults.Ok(new Response(DefaultEmailTemplates.All, TemplateRenderer.Variables)))
                .RequireCrmRead()
                .WithTags("Automations")
                .WithName("GetAutomationTemplates");
    }
}

/// <summary>POST /api/automations/rules y PUT /api/automations/rules/{id}.</summary>
public static class SaveRule
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("/automations/rules", async (RuleCommand command, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(null, command, ct)).Match(r => TypedResults.Created($"/api/automations/rules/{r.Id}", r)))
                .WithValidation<RuleCommand>()
                .RequireCrmWrite()
                .WithTags("Automations")
                .WithName("CreateAutomationRule");

            app.MapPut("/automations/rules/{id:guid}", async (Guid id, RuleCommand command, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(id, command, ct)).Match(TypedResults.Ok))
                .WithValidation<RuleCommand>()
                .RequireCrmWrite()
                .WithTags("Automations")
                .WithName("UpdateAutomationRule");
        }
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<RuleResponse>> HandleAsync(Guid? id, RuleCommand command, CancellationToken ct)
        {
            AutomationRule? rule;
            if (id is null)
            {
                rule = new AutomationRule { Name = command.Name.Trim() };
                db.AutomationRules.Add(rule);
            }
            else
            {
                rule = await db.AutomationRules.FirstOrDefaultAsync(r => r.Id == id, ct);
                if (rule is null)
                {
                    return Error.NotFound("AutomationRule.NotFound", "La regla no existe.");
                }

                rule.Name = command.Name.Trim();
            }

            rule.Trigger = command.Trigger;
            rule.Action = command.Action;
            rule.DelayMinutes = command.DelayMinutes;
            rule.IsActive = command.IsActive;
            rule.Configuration = NormalizeConfiguration(command);
            await db.SaveChangesAsync(ct);

            return RuleMapping.ToResponse(rule, 0, null);
        }

        private static string NormalizeConfiguration(RuleCommand command)
        {
            if (command.Action != AutomationAction.AddTag)
            {
                return command.Configuration.GetRawText();
            }

            var tag = CustomerTags.Normalize(command.Configuration.GetProperty("tag").GetString()!);
            return JsonSerializer.Serialize(new { tag });
        }
    }
}

/// <summary>PATCH /api/automations/rules/{id}/active — activar o pausar una regla.</summary>
public static class ToggleRule
{
    public sealed record Command(bool IsActive);

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPatch("/automations/rules/{id:guid}/active", async (Guid id, Command command, AppDbContext db, CancellationToken ct) =>
                    await db.AutomationRules.Where(r => r.Id == id)
                        .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, command.IsActive), ct) == 0
                        ? Error.NotFound("AutomationRule.NotFound", "La regla no existe.").ToProblem()
                        : TypedResults.NoContent())
                .RequireCrmWrite()
                .WithTags("Automations")
                .WithName("ToggleAutomationRule");
    }
}

/// <summary>DELETE /api/automations/rules/{id} — borra la regla y su historial.</summary>
public static class DeleteRule
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapDelete("/automations/rules/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
                    await db.AutomationRules.Where(r => r.Id == id).ExecuteDeleteAsync(ct) == 0
                        ? Error.NotFound("AutomationRule.NotFound", "La regla no existe.").ToProblem()
                        : TypedResults.NoContent())
                .RequireCrmWrite()
                .WithTags("Automations")
                .WithName("DeleteAutomationRule");
    }
}

internal static class RuleMapping
{
    public static RuleResponse ToResponse(AutomationRule rule, int executions, DateTimeOffset? lastExecutedAt)
    {
        using var json = JsonDocument.Parse(string.IsNullOrWhiteSpace(rule.Configuration) ? "{}" : rule.Configuration);
        return new RuleResponse(rule.Id, rule.Name, rule.Trigger, rule.Action, json.RootElement.Clone(), rule.DelayMinutes,
            rule.IsActive, AutomationTriggers.IsSupported(rule.Trigger), executions, lastExecutedAt);
    }
}
