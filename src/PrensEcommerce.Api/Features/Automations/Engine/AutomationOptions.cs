namespace PrensEcommerce.Api.Features.Automations.Engine;

public sealed class AutomationOptions
{
    public const string SectionName = "Automations";

    /// <summary>Activa el proceso en segundo plano que programa y ejecuta las reglas.</summary>
    public bool Enabled { get; set; } = true;

    public int IntervalSeconds { get; set; } = 60;

    /// <summary>
    /// "Simulated" (por defecto y único valor admitido hoy): nunca sale un correo real.
    /// El envío real (SMTP, Gmail…) será otra implementación de <see cref="IEmailSender"/>.
    /// </summary>
    public string EmailMode { get; set; } = "Simulated";

    /// <summary>Máximo de elementos procesados por tienda y pasada.</summary>
    public int BatchSize { get; set; } = 200;

    /// <summary>Un carrito más antiguo que esto ya no se considera recuperable.</summary>
    public int AbandonedCartMaxAgeDays { get; set; } = 7;
}
