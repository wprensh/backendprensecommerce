using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Authorization;

namespace PrensEcommerce.Api.Features.Roles.GetPermissions;

/// <summary>GET /api/admin/permissions — catálogo de permisos para pintar la matriz de roles.</summary>
public static class GetPermissions
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/admin/permissions", () => TypedResults.Ok(Permissions.All))
                .RequirePermission(Permissions.RolesRead)
                .WithTags(RoleRules.Tag)
                .WithName("GetPermissions");
    }
}
