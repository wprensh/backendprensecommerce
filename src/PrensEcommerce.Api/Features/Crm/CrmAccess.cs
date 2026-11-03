using PrensEcommerce.Api.Common.Authorization;

namespace PrensEcommerce.Api.Features.Crm;

/// <summary>
/// Punto único de autorización del CRM y las automatizaciones: permisos <c>crm.read</c> (consultar,
/// dejar notas) y <c>crm.write</c> (etiquetas, segmentos y reglas). El rol Admin los tiene todos.
/// </summary>
public static class CrmAccess
{
    public static TBuilder RequireCrmRead<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder => builder.RequirePermission(Permissions.CrmRead);

    public static TBuilder RequireCrmWrite<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder => builder.RequirePermission(Permissions.CrmWrite);
}
