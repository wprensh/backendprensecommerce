namespace PrensEcommerce.Api.Features.Automations.Engine;

public sealed record EmailMessage(string To, string Subject, string Body, string StoreName);

public enum EmailDelivery
{
    Simulated,
    Sent,
}

/// <summary>Canal de salida de correo. Cambiar de proveedor (SMTP, Gmail, SendGrid…) = nueva implementación.</summary>
public interface IEmailSender
{
    Task<EmailDelivery> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// Modo por defecto: NO envía nada. Deja traza en el log y la ejecución queda registrada con
/// el correo renderizado, marcado como simulado, para revisar qué se habría enviado.
/// </summary>
internal sealed class SimulatedEmailSender(ILogger<SimulatedEmailSender> logger) : IEmailSender
{
    public Task<EmailDelivery> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation("[Simulación] Correo de {Store} para {To}: {Subject}", message.StoreName, message.To, message.Subject);
        return Task.FromResult(EmailDelivery.Simulated);
    }
}
