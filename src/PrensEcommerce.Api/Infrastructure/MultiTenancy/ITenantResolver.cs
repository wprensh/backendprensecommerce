namespace PrensEcommerce.Api.Infrastructure.MultiTenancy;

/// <summary>
/// Estrategia para extraer el identificador del tenant de la petición (OCP:
/// añadir una estrategia nueva = registrar otra implementación, sin tocar el middleware).
/// Las estrategias se evalúan en el orden en que se registran.
/// </summary>
public interface ITenantResolver
{
    string? ResolveIdentifier(HttpContext context);
}
