using System.Globalization;
using System.Text.RegularExpressions;

namespace PrensEcommerce.Api.Features.Automations.Engine;

/// <summary>Sustituye variables <c>{{nombre}}</c> en asunto y cuerpo. Las variables desconocidas quedan vacías.</summary>
public static partial class TemplateRenderer
{
    public static readonly IReadOnlyList<string> Variables = ["nombre", "email", "tienda", "pedido", "total", "unidades"];

    private static readonly CultureInfo Money = CultureInfo.GetCultureInfo("es-ES");

    public static string Render(string template, IReadOnlyDictionary<string, string?> values) =>
        VariablePattern().Replace(template, match =>
            values.TryGetValue(match.Groups[1].Value.ToLowerInvariant(), out var value) ? value ?? string.Empty : string.Empty);

    public static string FormatMoney(decimal? amount) => amount?.ToString("N2", Money) ?? string.Empty;

    /// <summary>Número de pedido corto y legible (primeros 8 caracteres del Id).</summary>
    public static string ShortReference(Guid? id) =>
        id is { } value ? value.ToString("N")[..8].ToUpperInvariant() : string.Empty;

    [GeneratedRegex(@"\{\{\s*([a-zA-Z_]+)\s*\}\}")]
    private static partial Regex VariablePattern();
}

/// <summary>Plantillas incluidas, referenciables desde la configuración de una regla con <c>{"template":"welcome"}</c>.</summary>
public static class DefaultEmailTemplates
{
    public sealed record Template(string Key, string Name, string Subject, string Body);

    public static readonly IReadOnlyList<Template> All =
    [
        new("welcome", "Bienvenida",
            "¡Bienvenido/a a {{tienda}}, {{nombre}}!",
            "Hola {{nombre}},\n\nGracias por registrarte en {{tienda}}. Ya puedes descubrir nuestro catálogo y guardar tus productos favoritos.\n\nUn saludo,\nEl equipo de {{tienda}}"),
        new("order-confirmation", "Confirmación de pedido",
            "Hemos recibido tu pedido {{pedido}}",
            "Hola {{nombre}},\n\nHemos recibido tu pedido {{pedido}} por un total de {{total}} €. Te avisaremos cuando se confirme el pago.\n\nGracias por comprar en {{tienda}}."),
        new("order-paid", "Pago confirmado",
            "Pago confirmado: pedido {{pedido}}",
            "Hola {{nombre}},\n\nHemos confirmado el pago de tu pedido {{pedido}} ({{total}} €). Lo estamos preparando para el envío.\n\nEl equipo de {{tienda}}"),
        new("cart-reminder", "Carrito abandonado",
            "{{nombre}}, tus productos te esperan",
            "Hola {{nombre}},\n\nDejaste {{unidades}} producto(s) en tu carrito de {{tienda}}. Los hemos guardado para ti: vuelve cuando quieras para terminar tu compra.\n\nUn saludo."),
        new("ticket-closed", "Ticket cerrado",
            "Tu consulta en {{tienda}} está resuelta",
            "Hola {{nombre}},\n\nHemos cerrado tu consulta. Si necesitas algo más, responde a este correo o abre un nuevo ticket.\n\nEl equipo de atención al cliente de {{tienda}}"),
        new("winback", "Reactivación",
            "Te echamos de menos, {{nombre}}",
            "Hola {{nombre}},\n\nHace tiempo que no te vemos por {{tienda}}. Tenemos novedades que pueden interesarte: ¡pásate a verlas!\n\nUn saludo."),
    ];

    public static Template? Find(string? key) =>
        key is null ? null : All.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));
}
