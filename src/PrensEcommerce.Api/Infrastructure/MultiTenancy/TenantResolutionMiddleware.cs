using System.Security.Claims;
using Microsoft.Extensions.Options;
using PrensEcommerce.Api.Common.Security;

namespace PrensEcommerce.Api.Infrastructure.MultiTenancy;

/// <summary>
/// Identifica el tenant de cada petición y lo fija en el <see cref="ITenantSetter"/> Scoped.
/// Orden: header <c>X-Tenant-ID</c> → subdominio (según el orden de registro de los resolvers).
/// Si el usuario está autenticado, el tenant de su token debe coincidir con el resuelto.
/// </summary>
internal sealed class TenantResolutionMiddleware(
    RequestDelegate next,
    IOptions<MultiTenancyOptions> options,
    ILogger<TenantResolutionMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        IEnumerable<ITenantResolver> resolvers,
        ITenantStore store,
        ITenantSetter tenantSetter)
    {
        if (IsExcluded(context.Request.Path))
        {
            await next(context);
            return;
        }

        var identifier = resolvers
            .Select(r => r.ResolveIdentifier(context))
            .FirstOrDefault(id => id is not null);

        if (identifier is null)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Tenant requerido",
                $"Indica la tienda con el header '{options.Value.HeaderName}' o con el subdominio.");
            return;
        }

        var tenant = await store.FindAsync(identifier, context.RequestAborted);
        if (tenant is null)
        {
            logger.LogWarning("Tenant '{Identifier}' no encontrado o inactivo", identifier);
            await WriteProblemAsync(context, StatusCodes.Status404NotFound, "Tenant no encontrado",
                $"No existe una tienda activa con el identificador '{identifier}'.");
            return;
        }

        // Un token emitido para la tienda A no puede usarse contra la tienda B.
        var tokenTenant = context.User.FindFirstValue(AppClaims.TenantId);
        if (tokenTenant is not null && tokenTenant != tenant.Id.ToString())
        {
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "Tenant no válido",
                "El token no pertenece a esta tienda.");
            return;
        }

        tenantSetter.SetTenant(tenant);

        using (logger.BeginScope(new Dictionary<string, object> { ["TenantId"] = tenant.Id, ["Tenant"] = tenant.Slug }))
        {
            await next(context);
        }
    }

    private bool IsExcluded(PathString path) =>
        options.Value.ExcludedPaths.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));

    private static Task WriteProblemAsync(HttpContext context, int status, string title, string detail)
    {
        context.Response.StatusCode = status;
        return Results.Problem(statusCode: status, title: title, detail: detail).ExecuteAsync(context);
    }
}
