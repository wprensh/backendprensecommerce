using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Authorization;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Roles.DeleteRole;

/// <summary>DELETE /api/admin/roles/{id} — solo roles personalizados y sin usuarios asignados.</summary>
public static class DeleteRole
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapDelete($"{RoleRules.BasePath}/{{id:guid}}", async (Guid id, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(id, ct)).Match(_ => TypedResults.NoContent()))
                .RequirePermission(Permissions.RolesManage)
                .WithTags(RoleRules.Tag)
                .WithName("DeleteRole");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<bool>> HandleAsync(Guid id, CancellationToken ct)
        {
            var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);
            if (role is null)
            {
                return RoleRules.NotFound;
            }

            if (role.IsSystem)
            {
                return Error.Validation("Roles.SystemRoleDelete", "Los roles de sistema no se pueden eliminar.");
            }

            var assigned = await db.UserRoles.CountAsync(ur => ur.RoleId == id, ct);
            if (assigned > 0)
            {
                return Error.Conflict("Roles.InUse",
                    $"El rol está asignado a {assigned} usuario(s). Reasígnalos antes de eliminarlo.");
            }

            db.Roles.Remove(role);
            await db.SaveChangesAsync(ct);
            return true;
        }
    }
}
