using PrensEcommerce.Api.Features.Crm;

namespace PrensEcommerce.Api.Features.Automations.Engine;

public static class AutomationsServiceCollectionExtensions
{
    /// <summary>Registra CRM + automatizaciones: interceptor de eventos, motor, acciones, worker y canal de correo.</summary>
    public static IServiceCollection AddCrmAndAutomations(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AutomationOptions>()
            .Bind(configuration.GetSection(AutomationOptions.SectionName))
            .Validate(o => o.IntervalSeconds >= 5, "Automations:IntervalSeconds debe ser >= 5.")
            .Validate(o => o.BatchSize is > 0 and <= 5000, "Automations:BatchSize debe estar entre 1 y 5000.")
            .Validate(o => string.Equals(o.EmailMode, "Simulated", StringComparison.OrdinalIgnoreCase),
                "Automations:EmailMode solo admite 'Simulated': el envío real de correo aún no está habilitado.")
            .ValidateOnStart();

        services.AddSingleton<CustomerEventsInterceptor>();
        services.AddSingleton<IEmailSender, SimulatedEmailSender>();
        services.AddScoped<IAutomationActionHandler, SendEmailAction>();
        services.AddScoped<IAutomationActionHandler, AddTagAction>();
        services.AddScoped<AutomationEngine>();
        services.AddScoped<CartActivityTracker>();
        services.AddHostedService<AutomationWorker>();

        return services;
    }
}
