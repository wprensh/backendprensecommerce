using PrensEcommerce.Api.Domain;

namespace PrensEcommerce.Api.Features.Crm;

/// <summary>
/// Punto único de autorización del CRM y las automatizaciones.
/// Hoy se basa en los roles de sistema que ya viajan en el JWT (claim "role").
/// TODO: al integrar la administración de usuarios y roles (Common.Authorization), sustituir por
/// <c>RequirePermission(Permissions.CrmRead)</c> ("crm.read") y <c>RequirePermission(Permissions.CrmWrite)</c> ("crm.write").
/// </summary>
public static class CrmAccess
{
    public const string ReadPermission = "crm.read";

    public const string WritePermission = "crm.write";

    public static TBuilder RequireCrmRead<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(p => p.RequireRole(SystemRoles.Admin, SystemRoles.Marketing, SystemRoles.CustomerService));

    public static TBuilder RequireCrmWrite<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(p => p.RequireRole(SystemRoles.Admin, SystemRoles.Marketing));
}
