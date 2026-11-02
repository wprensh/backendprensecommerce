using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Features.Crm;

namespace PrensEcommerce.Api.Features.Automations.Engine;

/// <summary>Datos de una ejecución: la regla, el cliente y el payload (jsonb) del evento que la disparó.</summary>
public sealed record AutomationContext(AutomationRule Rule, User User, JsonObject Payload, string StoreName)
{
    public Guid? ReferenceId => Payload["referenceId"]?.GetValue<Guid?>();

    public decimal? Amount => Payload["amount"]?.GetValue<decimal?>();

    public int? Units => Payload["units"]?.GetValue<int?>();

    /// <summary>Configuración de la regla (columna jsonb) ya parseada; vacía si no es JSON válido.</summary>
    public JsonObject Configuration { get; } = ParseConfiguration(Rule.Configuration);

    private static JsonObject ParseConfiguration(string json)
    {
        try
        {
            return JsonNode.Parse(json) as JsonObject ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

public sealed record ActionOutcome(AutomationRunStatus Status, string? Error = null, JsonObject? Result = null)
{
    public static ActionOutcome Succeeded(JsonObject? result = null) => new(AutomationRunStatus.Succeeded, null, result);

    public static ActionOutcome Skipped(string reason) => new(AutomationRunStatus.Skipped, reason);

    public static ActionOutcome Failed(string error) => new(AutomationRunStatus.Failed, error);
}

/// <summary>
/// Ejecuta una <see cref="AutomationAction"/>. Para añadir una acción nueva (p. ej. AwardPoints desde
/// fidelización) basta con registrar otra implementación: el motor la elige por <see cref="Action"/>.
/// </summary>
public interface IAutomationActionHandler
{
    AutomationAction Action { get; }

    Task<ActionOutcome> ExecuteAsync(AutomationContext context, CancellationToken cancellationToken);
}

/// <summary>
/// SendEmail. Configuración: <c>{"template":"welcome"}</c> (plantilla incluida) o
/// <c>{"subject":"…","body":"…"}</c> (texto propio con variables).
/// </summary>
internal sealed class SendEmailAction(IEmailSender email) : IAutomationActionHandler
{
    public AutomationAction Action => AutomationAction.SendEmail;

    public async Task<ActionOutcome> ExecuteAsync(AutomationContext context, CancellationToken cancellationToken)
    {
        if (AutomationTriggers.IsMarketing(context.Rule.Trigger) && !context.User.AcceptsMarketing)
        {
            return ActionOutcome.Skipped("El cliente no acepta comunicaciones de marketing.");
        }

        if (!context.User.IsActive)
        {
            return ActionOutcome.Skipped("La cuenta del cliente está desactivada.");
        }

        var template = DefaultEmailTemplates.Find(context.Configuration["template"]?.GetValue<string>());
        var subject = context.Configuration["subject"]?.GetValue<string>() ?? template?.Subject;
        var body = context.Configuration["body"]?.GetValue<string>() ?? template?.Body;

        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body))
        {
            return ActionOutcome.Failed("La regla no tiene plantilla ni asunto/cuerpo configurados.");
        }

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["nombre"] = context.User.Name,
            ["email"] = context.User.Email,
            ["tienda"] = context.StoreName,
            ["pedido"] = TemplateRenderer.ShortReference(context.ReferenceId),
            ["total"] = TemplateRenderer.FormatMoney(context.Amount),
            ["unidades"] = context.Units?.ToString(CultureInfo.InvariantCulture),
        };

        var message = new EmailMessage(
            context.User.Email,
            TemplateRenderer.Render(subject, values),
            TemplateRenderer.Render(body, values),
            context.StoreName);

        var delivery = await email.SendAsync(message, cancellationToken);

        return ActionOutcome.Succeeded(new JsonObject
        {
            ["to"] = message.To,
            ["subject"] = message.Subject,
            ["body"] = message.Body,
            ["simulated"] = delivery == EmailDelivery.Simulated,
        });
    }
}

/// <summary>AddTag. Configuración: <c>{"tag":"vip"}</c>. Añade la etiqueta al cliente si no la tenía.</summary>
internal sealed class AddTagAction : IAutomationActionHandler
{
    public AutomationAction Action => AutomationAction.AddTag;

    public Task<ActionOutcome> ExecuteAsync(AutomationContext context, CancellationToken cancellationToken)
    {
        var raw = context.Configuration["tag"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Task.FromResult(ActionOutcome.Failed("Falta la etiqueta en la configuración (\"tag\")."));
        }

        var tag = CustomerTags.Normalize(raw);
        if (context.User.Tags.Contains(tag))
        {
            return Task.FromResult(ActionOutcome.Skipped($"El cliente ya tenía la etiqueta '{tag}'."));
        }

        // Lista nueva para que EF detecte el cambio en la columna text[].
        context.User.Tags = [.. context.User.Tags, tag];
        return Task.FromResult(ActionOutcome.Succeeded(new JsonObject { ["tag"] = tag }));
    }
}

public static class AutomationTriggers
{
    /// <summary>Disparadores comerciales: solo envían correo a clientes con consentimiento de marketing.</summary>
    public static bool IsMarketing(AutomationTrigger trigger) =>
        trigger is AutomationTrigger.CartAbandoned or AutomationTrigger.Inactivity or AutomationTrigger.Birthday;

    /// <summary>Disparadores que el motor sabe programar hoy.</summary>
    public static bool IsSupported(AutomationTrigger trigger) => trigger != AutomationTrigger.Birthday;
}
