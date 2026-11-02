using System.Security.Claims;
using PrensEcommerce.Api.Domain;

namespace PrensEcommerce.Api.Features.Catalog;

/// <summary>
/// Quién puede administrar el catálogo. Centralizado aquí para cambiarlo en un solo sitio
/// cuando la administración de roles exponga permisos (catalog.read / catalog.write).
/// </summary>
public static class CatalogAccess
{
    private static readonly string[] ManagerRoles = [SystemRoles.Admin, SystemRoles.CatalogManager];

    public static RouteHandlerBuilder RequireCatalogManager(this RouteHandlerBuilder builder) =>
        builder.RequireAuthorization(policy => policy.RequireRole(ManagerRoles));

    /// <summary>Los gestores ven también productos, categorías y marcas inactivos.</summary>
    public static bool IsCatalogManager(this ClaimsPrincipal user) => ManagerRoles.Any(user.IsInRole);
}
