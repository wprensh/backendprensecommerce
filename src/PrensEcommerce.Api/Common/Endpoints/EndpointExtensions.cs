using System.Reflection;
using PrensEcommerce.Api.Common.Abstractions;

namespace PrensEcommerce.Api.Common.Endpoints;

public static class EndpointExtensions
{
    /// <summary>Registra todos los <see cref="IEndpoint"/> e <see cref="IHandler"/> del ensamblado.</summary>
    public static IServiceCollection AddFeatureSlices(this IServiceCollection services, Assembly assembly)
    {
        foreach (var type in GetConcreteTypes<IEndpoint>(assembly))
        {
            services.AddSingleton(typeof(IEndpoint), type);
        }

        foreach (var type in GetConcreteTypes<IHandler>(assembly))
        {
            services.AddScoped(type);
        }

        return services;
    }

    /// <summary>Mapea los endpoints de todos los slices bajo el grupo indicado.</summary>
    public static IEndpointRouteBuilder MapFeatureSlices(this IEndpointRouteBuilder app, string prefix = "/api")
    {
        var group = app.MapGroup(prefix);

        foreach (var endpoint in app.ServiceProvider.GetServices<IEndpoint>())
        {
            endpoint.MapEndpoint(group);
        }

        return app;
    }

    private static IEnumerable<Type> GetConcreteTypes<T>(Assembly assembly) =>
        assembly.DefinedTypes.Where(t => t is { IsAbstract: false, IsInterface: false } && t.IsAssignableTo(typeof(T)));
}
