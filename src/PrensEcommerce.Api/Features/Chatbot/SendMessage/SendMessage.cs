using System.Security.Claims;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Features.Automations.Engine;
using PrensEcommerce.Api.Features.Checkout;
using PrensEcommerce.Api.Features.Loyalty;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Chatbot.SendMessage;

/// <summary>
/// POST /api/chatbot/messages — el asistente de la tienda responde a un mensaje del cliente.
/// Abierto a visitantes; con sesión iniciada también consulta sus pedidos y sus Prens Puntos.
/// Responde con datos reales del tenant (catálogo, pedidos, puntos, envío) sin IA externa.
/// </summary>
public static class SendChatMessage
{
    public const int MaxMessageLength = 500;

    /// <summary>Línea previa de la conversación, para que Claude tenga contexto (el motor por reglas la ignora).</summary>
    public sealed record HistoryLine(bool FromBot, string Text);

    public sealed record Command(string Message, IReadOnlyList<HistoryLine>? History = null);

    public const int MaxHistoryLines = 12;

    public sealed record ProductCard(
        Guid Id, string Name, string Category, decimal Price, int DiscountPercent, decimal FinalPrice, string ImageUrl, bool InStock);

    public sealed record OrderCard(Guid Id, string OrderNumber, OrderStatus Status, string StatusLabel, decimal Total, DateTimeOffset CreatedAt);

    public sealed record ChatLink(string Label, string Url);

    public sealed record Response(
        ChatIntent Intent,
        string Reply,
        IReadOnlyList<ProductCard> Products,
        IReadOnlyList<OrderCard> Orders,
        IReadOnlyList<ChatLink> Links,
        IReadOnlyList<string> Suggestions,
        bool CanEscalate);

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Message).NotEmpty().MaximumLength(MaxMessageLength);
            RuleFor(c => c.History!.Count).LessThanOrEqualTo(MaxHistoryLines).When(c => c.History is not null);
            RuleForEach(c => c.History).ChildRules(line => line.RuleFor(l => l.Text).NotEmpty().MaximumLength(2000));
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("/chatbot/messages", async (Command command, ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                    TypedResults.Ok(await handler.HandleAsync(
                        user.Identity?.IsAuthenticated == true ? user.GetUserId() : null, command, ct)))
                .WithValidation<Command>()
                .RequireRateLimiting(ChatbotRateLimit.PolicyName)
                .WithTags("Chatbot")
                .WithName("SendChatMessage");
    }

    /// <summary>
    /// Elige el motor: Claude (si está configurado) o el asistente por reglas. Si Claude no tiene
    /// API key, falla o declina responder, contesta el asistente por reglas para no dejar al cliente sin respuesta.
    /// </summary>
    public sealed class Handler(
        RulesAssistant rules,
        ClaudeAssistant claude,
        IOptions<ChatbotOptions> options,
        ILogger<Handler> logger) : IHandler
    {
        public async Task<Response> HandleAsync(Guid? userId, Command command, CancellationToken ct)
        {
            if (options.Value.UsesClaude && claude.IsConfigured)
            {
                try
                {
                    if (await claude.ReplyAsync(userId, command, ct) is { } reply)
                    {
                        return reply;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Claude no respondió; contesta el asistente por reglas.");
                }
            }

            return await rules.ReplyAsync(userId, command.Message, ct);
        }
    }

    /// <summary>Asistente sin IA: intención por palabras clave y datos reales de la tienda. También son las herramientas de Claude.</summary>
    public sealed class RulesAssistant(
        AppDbContext db,
        LoyaltyService loyalty,
        IOptions<CheckoutOptions> checkoutOptions) : IHandler
    {
        private static readonly string[] DefaultSuggestions =
            ["Buscar un producto", "¿Dónde está mi pedido?", "Mis Prens Puntos", "Costo de envío"];

        public async Task<Response> ReplyAsync(Guid? userId, string rawMessage, CancellationToken ct)
        {
            var message = rawMessage.Trim();
            var intent = ChatIntentClassifier.Classify(message);

            return intent switch
            {
                ChatIntent.Greeting => Text(intent,
                    "¡Hola! Soy el asistente de Prens Tecnología SHOP. Puedo buscar productos, contarte el estado de tus pedidos, " +
                    "tus Prens Puntos, el envío y los medios de pago. ¿En qué te ayudo?"),
                ChatIntent.Thanks => Text(intent, "¡Con gusto! Si necesitas algo más, aquí estoy."),
                ChatIntent.Help => Text(intent,
                    "Puedo ayudarte con: buscar productos (\"busco audífonos\"), el estado de un pedido (escribe su número, " +
                    "p. ej. PR-20261102-ABC123), tus Prens Puntos, el costo y tiempo de envío, los medios de pago y devoluciones. " +
                    "Si prefieres, te paso con una persona del equipo de soporte."),
                ChatIntent.ProductSearch => await SearchProductsAsync(message, ct),
                ChatIntent.OrderStatus => await OrderStatusAsync(userId, message, ct),
                ChatIntent.Loyalty => await LoyaltyAsync(userId, ct),
                ChatIntent.Shipping => Shipping(),
                ChatIntent.Payment => Payment(),
                ChatIntent.Returns => Returns(userId),
                ChatIntent.HumanAgent => HumanAgent(userId),
                _ => new Response(intent,
                    "No estoy seguro de haberte entendido. Puedes preguntarme por un producto, tu pedido, tus puntos o el envío, " +
                    "o pedirme que te pase con una persona del equipo.",
                    [], [], [], DefaultSuggestions, CanEscalate: userId is not null),
            };
        }

        public async Task<Response> SearchProductsAsync(string message, CancellationToken ct)
        {
            var terms = ChatIntentClassifier.SearchTerms(message);
            var products = db.Products.AsNoTracking().Where(p => p.IsActive && p.Category!.IsActive);

            List<ProductCard> found;
            if (terms.Count == 0)
            {
                // "¿Qué ofertas tienen?" sin producto concreto: lo más rebajado y mejor valorado.
                found = await products
                    .OrderByDescending(p => p.DiscountPercent).ThenByDescending(p => p.Rating)
                    .Take(4).Select(ToCard).ToListAsync(ct);

                return new Response(ChatIntent.ProductSearch,
                    found.Count == 0 ? "Aún no hay productos publicados en la tienda." : "Estas son algunas de nuestras mejores ofertas:",
                    found, [], [new("Ver todo el catálogo", "/catalog")], ["Busco audífonos", "Costo de envío"], CanEscalate: false);
            }

            // Un producto coincide si algún término aparece en su nombre, categoría, marca o descripción
            // (ILIKE ANY en PostgreSQL). Las vocales y la "n" van como comodín para que "audifonos" encuentre
            // "Audífonos" sin depender de la extensión unaccent; luego se descartan falsos positivos en memoria.
            var expanded = ChatIntentClassifier.WithSynonyms(terms);
            var patterns = expanded.Select(AccentTolerantPattern).ToArray();
            var candidates = await products
                .Where(p => patterns.Any(x => EF.Functions.ILike(p.Name, x)) ||
                            patterns.Any(x => EF.Functions.ILike(p.Category!.Name, x)) ||
                            (p.Brand != null && patterns.Any(x => EF.Functions.ILike(p.Brand.Name, x))) ||
                            patterns.Any(x => EF.Functions.ILike(p.Description, x)))
                .OrderByDescending(p => p.Rating)
                .Take(100)
                .Select(p => new
                {
                    p.Id, p.Name, Category = p.Category!.Name, Brand = p.Brand != null ? p.Brand.Name : "",
                    p.Description, p.Price, p.DiscountPercent, p.ImageUrl, p.Stock,
                })
                .ToListAsync(ct);

            found = candidates
                .Select(c => new
                {
                    Card = new ProductCard(c.Id, c.Name, c.Category, c.Price, c.DiscountPercent,
                        FinalPrice(c.Price, c.DiscountPercent), c.ImageUrl, c.Stock > 0),
                    InName = CountMatches(expanded, c.Name),
                    Anywhere = CountMatches(expanded, $"{c.Name} {c.Category} {c.Brand} {c.Description}"),
                })
                .Where(c => c.Anywhere > 0)
                .OrderByDescending(c => c.InName)
                .ThenByDescending(c => c.Anywhere)
                .ThenByDescending(c => c.Card.InStock)
                .Take(4)
                .Select(c => c.Card)
                .ToList();

            var query = string.Join(' ', terms);
            var catalogLink = new ChatLink($"Ver resultados de \"{query}\"", $"/catalog?search={Uri.EscapeDataString(query)}");

            return found.Count == 0
                ? new Response(ChatIntent.ProductSearch,
                    $"No encontré productos para \"{query}\". Prueba con otra palabra o revisa el catálogo completo.",
                    [], [], [new("Ver catálogo", "/catalog")], ["Ver ofertas", "Hablar con una persona"], CanEscalate: false)
                : new Response(ChatIntent.ProductSearch,
                    found.Count == 1 ? "Encontré este producto:" : $"Encontré estos {found.Count} productos:",
                    found, [], [catalogLink], ["Costo de envío", "Medios de pago"], CanEscalate: false);
        }

        public async Task<Response> OrderStatusAsync(Guid? userId, string message, CancellationToken ct)
        {
            if (userId is not { } id)
            {
                return new Response(ChatIntent.OrderStatus,
                    "Para consultar tus pedidos necesito que inicies sesión. Después podrás ver su estado aquí o en \"Mis pedidos\".",
                    [], [], [new("Iniciar sesión", "/auth/login")], [], CanEscalate: false);
            }

            var orders = db.Orders.AsNoTracking().Where(o => o.UserId == id);
            var number = ChatIntentClassifier.OrderNumber(message);
            if (number is not null)
            {
                orders = orders.Where(o => o.OrderNumber == number);
            }

            var found = await orders
                .OrderByDescending(o => o.CreatedAt)
                .Take(3)
                .Select(o => new { o.Id, o.OrderNumber, o.Status, o.Total, o.CreatedAt })
                .ToListAsync(ct);
            var cards = found.Select(o => new OrderCard(o.Id, o.OrderNumber, o.Status, StatusLabel(o.Status), o.Total, o.CreatedAt)).ToList();

            if (cards.Count == 0)
            {
                return new Response(ChatIntent.OrderStatus,
                    number is null
                        ? "Todavía no tienes pedidos. Cuando compres, aquí podrás ver su estado."
                        : $"No encontré el pedido {number} en tu cuenta. Revisa el número o abre un caso y lo buscamos.",
                    [], [], [new("Ir al catálogo", "/catalog")], [], CanEscalate: number is not null);
            }

            var first = cards[0];
            var reply = number is not null || cards.Count == 1
                ? $"Tu pedido {first.OrderNumber} está {first.StatusLabel.ToLowerInvariant()}. {StatusDetail(first.Status)}"
                : $"Estos son tus últimos pedidos. El más reciente, {first.OrderNumber}, está {first.StatusLabel.ToLowerInvariant()}.";

            return new Response(ChatIntent.OrderStatus, reply.Trim(), [], cards,
                [new("Ver mis pedidos", "/cuenta/pedidos")], ["Costo de envío", "Quiero devolver un producto"], CanEscalate: true);
        }

        public async Task<Response> LoyaltyAsync(Guid? userId, CancellationToken ct)
        {
            var options = loyalty.Options;
            var rules = $"Ganas 1 punto por cada {TemplateRenderer.FormatMoney(options.PesosPerPoint)} de compra y cada punto vale " +
                        $"{TemplateRenderer.FormatMoney(options.PointValue)} al canjearlo (desde {Points(options.MinRedeemPoints)} puntos).";

            if (userId is not { } id)
            {
                return new Response(ChatIntent.Loyalty,
                    $"Prens Puntos es nuestro programa de fidelización. {rules} Inicia sesión para ver tu saldo.",
                    [], [], [new("Iniciar sesión", "/auth/login"), new("Conocer Prens Puntos", "/loyalty")], [], CanEscalate: false);
            }

            var account = await db.LoyaltyAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == id, ct);
            var balance = account?.PointsBalance ?? 0;
            var lifetime = account?.LifetimePoints ?? 0;
            var tier = options.RuleFor(lifetime);
            var next = options.NextRuleFor(lifetime);
            var value = Math.Round(balance * options.PointValue, 0, MidpointRounding.AwayFromZero);

            var reply = $"Tienes {Points(balance)} Prens Puntos (equivalen a {TemplateRenderer.FormatMoney(value)}) y estás en nivel {TierLabel(tier.Tier)}.";
            if (next is not null)
            {
                reply += $" Te faltan {Points(next.MinLifetimePoints - lifetime)} puntos para llegar a {TierLabel(next.Tier)}.";
            }

            reply += balance >= options.MinRedeemPoints
                ? " Ya puedes canjearlos por un cupón de descuento."
                : $" Podrás canjearlos desde {Points(options.MinRedeemPoints)} puntos.";

            return new Response(ChatIntent.Loyalty, reply, [], [],
                [new("Ver mis Prens Puntos", "/loyalty")], ["Buscar un producto"], CanEscalate: false);
        }

        public Response Shipping()
        {
            var options = checkoutOptions.Value;
            var reply = $"El envío a domicilio cuesta {TemplateRenderer.FormatMoney(options.ShippingCost)}";
            reply += options.FreeShippingFrom is { } free
                ? $" y es gratis en compras desde {TemplateRenderer.FormatMoney(free)}."
                : ".";
            reply += " Una vez confirmado el pago preparamos tu pedido y te avisamos cuando salga hacia tu dirección.";

            return new Response(ChatIntent.Shipping, reply, [], [], [new("Ver mi carrito", "/cart")],
                ["Medios de pago", "¿Dónde está mi pedido?"], CanEscalate: false);
        }

        public static Response Payment() => new(ChatIntent.Payment,
            "Puedes pagar con tarjeta de crédito o débito, transferencia o efectivo. El pago con tarjeta se procesa al confirmar " +
            "el pedido y tus Prens Puntos se pueden usar como cupón de descuento en el checkout.",
            [], [], [new("Ir al carrito", "/cart")], ["Costo de envío", "Mis Prens Puntos"], CanEscalate: false);

        public static Response Returns(Guid? userId) => new(ChatIntent.Returns,
            "Si un producto llegó dañado o no es lo que esperabas, abre un caso de soporte indicando el pedido y te ayudamos " +
            "con la devolución o el cambio." + (userId is null ? " Para abrir el caso necesitas iniciar sesión." : ""),
            [], [],
            userId is null ? [new("Iniciar sesión", "/auth/login")] : [new("Abrir un caso", "/support/new")],
            [], CanEscalate: userId is not null);

        private static Response HumanAgent(Guid? userId) => userId is null
            ? new Response(ChatIntent.HumanAgent,
                "Para hablar con una persona del equipo inicia sesión y te pasaremos la conversación a soporte.",
                [], [], [new("Iniciar sesión", "/auth/login")], [], CanEscalate: false)
            : new Response(ChatIntent.HumanAgent,
                "Claro. Pulsa \"Hablar con una persona\" y enviaré esta conversación al equipo de soporte; te responderán en \"Ayuda y soporte\".",
                [], [], [], [], CanEscalate: true);

        private static Response Text(ChatIntent intent, string reply) =>
            new(intent, reply, [], [], [], DefaultSuggestions, CanEscalate: false);

        private static readonly System.Globalization.NumberFormatInfo Thousands = new() { NumberGroupSeparator = "." };

        private static string Points(int value) => value.ToString("N0", Thousands);

        private static decimal FinalPrice(decimal price, int discountPercent) =>
            Math.Round(price * (100 - discountPercent) / 100m, 0, MidpointRounding.AwayFromZero);

        private static int CountMatches(IReadOnlyList<string> terms, string text)
        {
            var normalized = ChatIntentClassifier.Normalize(text);
            return terms.Count(t => normalized.Contains(t, StringComparison.Ordinal));
        }

        /// <summary>"audifono" → "%aud_f_n_%": tolera tildes y la ñ sin extensiones de PostgreSQL.</summary>
        public static string AccentTolerantPattern(string term) =>
            "%" + string.Concat(term.Select(c => c is 'a' or 'e' or 'i' or 'o' or 'u' or 'n' ? '_' : c)) + "%";

        private static readonly System.Linq.Expressions.Expression<Func<Product, ProductCard>> ToCard = p => new ProductCard(
            p.Id, p.Name, p.Category!.Name, p.Price, p.DiscountPercent,
            Math.Round(p.Price * (100 - p.DiscountPercent) / 100m, 0, MidpointRounding.AwayFromZero), p.ImageUrl, p.Stock > 0);

        public static string StatusLabel(OrderStatus status) => status switch
        {
            OrderStatus.Pending => "Pendiente de pago",
            OrderStatus.Paid => "Pagado",
            OrderStatus.Shipped => "Enviado",
            OrderStatus.Delivered => "Entregado",
            OrderStatus.Cancelled => "Cancelado",
            _ => status.ToString(),
        };

        private static string StatusDetail(OrderStatus status) => status switch
        {
            OrderStatus.Pending => "Aún no recibimos el pago; si ya pagaste, puede tardar unos minutos en reflejarse.",
            OrderStatus.Paid => "Estamos preparando tu pedido para enviarlo.",
            OrderStatus.Shipped => "Ya va en camino a tu dirección.",
            OrderStatus.Delivered => "¡Esperamos que lo disfrutes!",
            OrderStatus.Cancelled => "Si no esperabas esta cancelación, abre un caso y lo revisamos.",
            _ => string.Empty,
        };

        private static string TierLabel(LoyaltyTier tier) => tier switch
        {
            LoyaltyTier.Bronze => "Bronce",
            LoyaltyTier.Silver => "Plata",
            LoyaltyTier.Gold => "Oro",
            LoyaltyTier.Platinum => "Platino",
            _ => tier.ToString(),
        };
    }
}
