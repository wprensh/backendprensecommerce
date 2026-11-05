using System.Text;
using System.Text.RegularExpressions;

namespace PrensEcommerce.Api.Features.Chatbot;

/// <summary>Lo que el cliente quiere hacer con su mensaje.</summary>
public enum ChatIntent
{
    Unknown = 0,
    Greeting,
    Thanks,
    Help,
    ProductSearch,
    OrderStatus,
    Loyalty,
    Shipping,
    Payment,
    Returns,
    HumanAgent,
}

/// <summary>
/// Clasificador por palabras clave, sin IA externa: rápido, gratis y predecible.
/// Trabaja sobre texto normalizado (minúsculas, sin tildes) para tolerar "envío"/"envio".
/// </summary>
public static partial class ChatIntentClassifier
{
    // Orden = prioridad: si un mensaje menciona "pedido" y "devolver", gana la devolución.
    private static readonly (ChatIntent Intent, string[] Keywords)[] Rules =
    [
        (ChatIntent.HumanAgent, ["agente", "asesor", "humano", "persona real", "hablar con alguien", "operador", "abrir un caso", "abrir caso", "queja", "reclamo"]),
        (ChatIntent.Returns, ["devolu", "devolver", "reembolso", "garantia", "cambiar un producto", "cambio de producto", "defectuoso", "llego roto", "llego danado"]),
        (ChatIntent.OrderStatus, ["pedido", "orden ", "ordenes", "compra ", "mis compras", "mi compra", "seguimiento", "rastrear", "rastreo", "donde esta mi", "no ha llegado", "no llega", "estado de mi"]),
        (ChatIntent.Loyalty, ["punto", "prens puntos", "fideliz", "nivel", "cupon", "canjear", "canje", "recompensa"]),
        (ChatIntent.Shipping, ["envio", "enviar", "despacho", "domicilio", "entrega", "cuanto tarda", "cuanto demora", "flete"]),
        (ChatIntent.Payment, ["pago", "pagar", "tarjeta", "transferencia", "efectivo", "contraentrega", "metodo de pago", "medios de pago", "cuotas", "pse"]),
        (ChatIntent.ProductSearch, ["busco", "buscar", "quiero", "necesito", "tienen", "tienes", "hay ", "venden", "precio", "cuanto cuesta", "cuanto vale", "producto", "recomienda", "recomiendame", "oferta", "descuento", "disponible", "stock"]),
        (ChatIntent.Thanks, ["gracias", "muchas gracias", "te agradezco", "genial", "perfecto"]),
        (ChatIntent.Help, ["ayuda", "que puedes hacer", "que haces", "opciones", "menu"]),
        (ChatIntent.Greeting, ["hola", "buenas", "buenos dias", "buen dia", "hey", "saludos", "que tal"]),
    ];

    /// <summary>Palabras que no aportan a una búsqueda de productos.</summary>
    private static readonly HashSet<string> StopWords =
    [
        "a", "al", "algo", "algun", "alguna", "alguno", "algunos", "busco", "buscar", "buscando", "como", "con", "cual", "cuales",
        "cuanto", "cuesta", "de", "del", "disponible", "disponibles", "el", "en", "es", "esta", "estan", "hay", "hola",
        "la", "las", "lo", "los", "me", "mi", "necesito", "para", "por", "precio", "precios", "producto", "productos",
        "que", "quiero", "recomienda", "recomiendame", "se", "su", "sus", "tiene", "tienen", "tienes", "un", "una",
        "unas", "uno", "unos", "vale", "venden", "y", "ver", "mostrar", "muestrame", "favor", "porfa", "gracias",
        "buenas", "buenos", "dias", "oferta", "ofertas", "descuento", "descuentos", "stock", "barato", "baratos", "economico",
    ];

    /// <summary>Cómo llaman los clientes a lo mismo en Colombia y España ("audífonos" = "auriculares").</summary>
    private static readonly Dictionary<string, string[]> Synonyms = new()
    {
        ["audifono"] = ["auricular", "headset"],
        ["auricular"] = ["audifono"],
        ["celular"] = ["telefono", "movil", "smartphone"],
        ["movil"] = ["celular", "telefono", "smartphone"],
        ["portatil"] = ["laptop", "computador", "notebook"],
        ["laptop"] = ["portatil", "computador"],
        ["computador"] = ["ordenador", "portatil", "pc"],
        ["ordenador"] = ["computador", "pc"],
        ["tenis"] = ["zapatilla"],
        ["zapatilla"] = ["tenis"],
        ["chaqueta"] = ["chamarra", "abrigo"],
        ["parlante"] = ["altavoz", "bafle"],
        ["altavoz"] = ["parlante"],
        ["reloj"] = ["smartwatch"],
    };

    /// <summary>Términos de búsqueda más sus sinónimos, sin repetir.</summary>
    public static IReadOnlyList<string> WithSynonyms(IReadOnlyList<string> terms) =>
        terms.SelectMany(t => Synonyms.TryGetValue(t, out var alt) ? [t, .. alt] : new[] { t })
            .Distinct()
            .ToList();

    public static ChatIntent Classify(string message)
    {
        var text = Normalize(message);
        if (text.Length == 0)
        {
            return ChatIntent.Unknown;
        }

        if (OrderNumberPattern().IsMatch(message))
        {
            return ChatIntent.OrderStatus;
        }

        // Cada palabra clave debe empezar una palabra; si termina en espacio, debe ser la palabra completa
        // ("orden " no coincide con "ordenador", "compra " no coincide con "comprar").
        var padded = $" {text} ";
        foreach (var (intent, keywords) in Rules)
        {
            if (keywords.Any(k => padded.Contains($" {k}", StringComparison.Ordinal)))
            {
                return intent;
            }
        }

        // Palabras con contenido sin otra intención ("audifonos bluetooth") suelen ser una búsqueda.
        return SearchTerms(message).Count > 0 ? ChatIntent.ProductSearch : ChatIntent.Unknown;
    }

    /// <summary>Términos útiles para buscar en el catálogo ("¿Tienen audífonos baratos?" → ["audifonos"]).</summary>
    public static IReadOnlyList<string> SearchTerms(string message) =>
        Normalize(message)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3 && !StopWords.Contains(w))
            .Select(Singular)
            .Distinct()
            .Take(5)
            .ToList();

    /// <summary>Número de pedido escrito por el cliente (p. ej. <c>PR-20261102-7K3QZD</c>), en mayúsculas.</summary>
    public static string? OrderNumber(string message) =>
        OrderNumberPattern().Match(message) is { Success: true } m ? m.Value.ToUpperInvariant() : null;

    /// <summary>Minúsculas, sin tildes y solo letras, dígitos y espacios simples.</summary>
    public static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        var lastWasSpace = true;
        foreach (var raw in value.ToLowerInvariant())
        {
            var c = raw switch
            {
                'á' or 'à' or 'ä' or 'â' => 'a',
                'é' or 'è' or 'ë' or 'ê' => 'e',
                'í' or 'ì' or 'ï' or 'î' => 'i',
                'ó' or 'ò' or 'ö' or 'ô' => 'o',
                'ú' or 'ù' or 'ü' or 'û' => 'u',
                'ñ' => 'n',
                _ => raw,
            };

            if (char.IsAsciiLetterOrDigit(c))
            {
                builder.Append(c);
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    /// <summary>Plural simple del español para ampliar coincidencias ("lamparas" → "lampara", "auriculares" → "auricular").</summary>
    private static string Singular(string word) =>
        word.Length > 5 && word.EndsWith("es", StringComparison.Ordinal) && !word.EndsWith("ees", StringComparison.Ordinal)
            ? word[..^2]
            : word.Length > 4 && word.EndsWith('s')
                ? word[..^1]
                : word;

    [GeneratedRegex(@"\bPR-\d{8}-[A-Z0-9]{6}\b", RegexOptions.IgnoreCase)]
    private static partial Regex OrderNumberPattern();
}
