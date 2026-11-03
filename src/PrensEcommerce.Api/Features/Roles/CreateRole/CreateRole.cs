using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Authorization;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Roles.CreateRole;

/// <summary>POST /api/admin/roles</summary>
public static class CreateRole
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost(RoleRules.BasePath, async (RoleCommand command, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(command, ct))
                    .Match(role => TypedResults.Created($"/api{RoleRules.BasePath}/{role.Id}", role)))
                .WithValidation<RoleCommand>()
                .RequirePermission(Permissions.RolesManage)
                .WithTags(RoleRules.Tag)
                .WithName("CreateRole");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<RoleResponse>> HandleAsync(RoleCommand command, CancellationToken ct)
        {
            var name = command.Name.Trim();
            if (await RoleRules.NameInUseAsync(db, name, null, ct))
            {
                return Error.Conflict("Roles.NameInUse", "Ya existe un rol con ese nombre.");
            }

            var role = new Role
            {
                Name = name,
                Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
                Permissions = RoleRules.Normalize(command.Permissions),
            };

            db.Roles.Add(role);
            await db.SaveChangesAsync(ct);

            return new RoleResponse(role.Id, role.Name, role.Description, role.IsSystem, role.Permissions, 0);
        }
    }
}
