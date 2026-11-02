namespace PrensEcommerce.Api.Infrastructure.MultiTenancy;

public static class MultiTenancyExtensions
{
    /// <summary>
    /// Registra la multi-tenencia en el contenedor de DI:
    /// <list type="bullet">
    /// <item><see cref="TenantProvider"/> como Scoped, expuesto como <see cref="ITenantProvider"/> (lectura)
    /// y <see cref="ITenantSetter"/> (escritura) sobre la MISMA instancia por petición.</item>
    /// <item>Estrategias de resolución en orden de prioridad: header, luego subdominio.</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddMultiTenancy(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MultiTenancyOptions>()
            .Bind(configuration.GetSection(MultiTenancyOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.HeaderName), "MultiTenancy:HeaderName es obligatorio.")
            .ValidateOnStart();

        services.AddScoped<TenantProvider>();
        services.AddScoped<ITenantProvider>(sp => sp.GetRequiredService<TenantProvider>());
        services.AddScoped<ITenantSetter>(sp => sp.GetRequiredService<TenantProvider>());

        services.AddSingleton<ITenantResolver, HeaderTenantResolver>();
        services.AddSingleton<ITenantResolver, SubdomainTenantResolver>();

        services.AddScoped<ITenantStore, CachedTenantStore>();

        return services;
    }

    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantResolutionMiddleware>();
}
