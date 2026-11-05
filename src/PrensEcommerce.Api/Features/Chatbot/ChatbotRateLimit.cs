using System.Threading.RateLimiting;

namespace PrensEcommerce.Api.Features.Chatbot;

/// <summary>
/// El chatbot no exige sesión, así que se limita por IP para que nadie lo use para
/// recorrer el catálogo a ráfagas: 20 mensajes por minuto, el resto recibe 429.
/// </summary>
public static class ChatbotRateLimit
{
    public const string PolicyName = "chatbot";

    public const int PermitsPerMinute = 20;

    public static IServiceCollection AddChatbotRateLimit(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(PolicyName, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = PermitsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });
}
