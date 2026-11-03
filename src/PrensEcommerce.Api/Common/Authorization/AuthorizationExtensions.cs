namespace PrensEcommerce.Api.Common.Authorization;

public static class AuthorizationExtensions
{
    /// <summary>Política para "personal de la tienda": cualquier usuario con al menos un permiso.</summary>
    public const string StaffPolicy = "staff";

    /// <summary>Registra una política por permiso (exige el claim <c>perm</c> con ese código) y la política staff.</summary>
    public static IServiceCollection AddPermissionPolicies(this IServiceCollection services)
    {
        var builder = services.AddAuthorizationBuilder();

        builder.AddPolicy(StaffPolicy, policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim(AppPermissionClaims.Permission));

        foreach (var permission in Permissions.All)
        {
            builder.AddPolicy(permission.Code, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(AppPermissionClaims.Permission, permission.Code));
        }

        return services;
    }

    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(permission);
}
