using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Authorization;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Roles.UpdateRole;

/// <summary>
/// PUT /api/admin/roles/{id}. Los roles de sistema no se pueden renombrar y el rol Admin
/// conserva siempre todos los permisos ("*"). Los cambios se aplican a los usuarios en su próximo login.
/// </summary>
public static class UpdateRole
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPut($"{RoleRules.BasePath}/{{id:guid}}", async (Guid id, RoleCommand command, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(id, command, ct)).Match(TypedResults.Ok))
                .WithValidation<RoleCommand>()
                .RequirePermission(Permissions.RolesManage)
                .WithTags(RoleRules.Tag)
                .WithName("UpdateRole");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<RoleResponse>> HandleAsync(Guid id, RoleCommand command, CancellationToken ct)
        {
            var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);
            if (role is null)
            {
                return RoleRules.NotFound;
            }

            var name = command.Name.Trim();
            if (role.IsSystem && name != role.Name)
            {
                return Error.Validation("Roles.SystemRoleRename", "Los roles de sistema no se pueden renombrar.");
            }

            if (name != role.Name && await RoleRules.NameInUseAsync(db, name, id, ct))
            {
                return Error.Conflict("Roles.NameInUse", "Ya existe un rol con ese nombre.");
            }

            role.Name = name;
            role.Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim();
            if (role is not { IsSystem: true, Name: SystemRoles.Admin })
            {
                role.Permissions = RoleRules.Normalize(command.Permissions);
            }

            await db.SaveChangesAsync(ct);
            return (await RoleRules.FindAsync(db, id, ct))!;
        }
    }
}
