using System.Security.Claims;
using PrensEcommerce.Api.Common.Authorization;

namespace PrensEcommerce.Api.Features.Catalog;

/// <summary>Quién puede administrar el catálogo: permiso <c>catalog.write</c> (el rol Admin lo tiene vía "*").</summary>
public static class CatalogAccess
{
    public static RouteHandlerBuilder RequireCatalogManager(this RouteHandlerBuilder builder) =>
        builder.RequirePermission(Permissions.CatalogWrite);

    /// <summary>Quien puede leer la administración del catálogo ve también productos, categorías y marcas inactivos.</summary>
    public static bool IsCatalogManager(this ClaimsPrincipal user) =>
        user.HasClaim(AppPermissionClaims.Permission, Permissions.CatalogRead) ||
        user.HasClaim(AppPermissionClaims.Permission, Permissions.CatalogWrite);
}
