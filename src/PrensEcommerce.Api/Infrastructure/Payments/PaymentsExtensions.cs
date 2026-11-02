namespace PrensEcommerce.Api.Infrastructure.Payments;

public static class PaymentsExtensions
{
    /// <summary>
    /// Registra la pasarela indicada en <c>Payments:Provider</c>. Para añadir un proveedor real
    /// basta con implementar <see cref="IPaymentGateway"/> y añadir su caso aquí.
    /// </summary>
    public static IServiceCollection AddPayments(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PaymentOptions>()
            .Bind(configuration.GetSection(PaymentOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var provider = configuration[$"{PaymentOptions.SectionName}:Provider"] ?? SimulatedPaymentGateway.ProviderName;

        switch (provider.ToLowerInvariant())
        {
            case SimulatedPaymentGateway.ProviderName:
                services.AddScoped<IPaymentGateway, SimulatedPaymentGateway>();
                break;
            default:
                throw new InvalidOperationException($"Pasarela de pago desconocida: '{provider}'.");
        }

        return services;
    }
}
