using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Authorization;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Roles.GetRoles;

/// <summary>GET /api/admin/roles — todos los roles de la tienda (son pocos: sin paginación).</summary>
public static class GetRoles
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet(RoleRules.BasePath, async (Handler handler, CancellationToken ct) =>
                    TypedResults.Ok(await handler.HandleAsync(ct)))
                // Quien gestiona usuarios necesita la lista de roles para asignarlos.
                .RequireAuthorization(p => p.RequireClaim(AppPermissionClaims.Permission, Permissions.RolesRead, Permissions.UsersManage))
                .WithTags(RoleRules.Tag)
                .WithName("GetRoles");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<IReadOnlyList<RoleResponse>> HandleAsync(CancellationToken ct) =>
            await db.Roles.AsNoTracking()
                .OrderByDescending(r => r.IsSystem).ThenBy(r => r.Name)
                .Select(RoleResponse.Projection(db))
                .ToListAsync(ct);
    }
}
