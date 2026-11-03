using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Authorization;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Users.SetUserStatus;

/// <summary>
/// PATCH /api/admin/users/{id}/status — activa o desactiva. No hay borrado físico:
/// los usuarios tienen pedidos e historial que deben conservarse.
/// </summary>
public static class SetUserStatus
{
    public sealed record Command(bool IsActive);

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPatch($"{UserRules.BasePath}/{{id:guid}}/status",
                    async (Guid id, Command command, Handler handler, ClaimsPrincipal principal, CancellationToken ct) =>
                        (await handler.HandleAsync(id, command, principal.GetUserId(), ct)).Match(TypedResults.Ok))
                .RequirePermission(Permissions.UsersManage)
                .WithTags(UserRules.Tag)
                .WithName("SetUserStatus");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<UserResponse>> HandleAsync(Guid id, Command command, Guid currentUserId, CancellationToken ct)
        {
            if (id == currentUserId && !command.IsActive)
            {
                return Error.Validation("Users.CannotDisableSelf", "No puedes desactivar tu propia cuenta.");
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
            if (user is null)
            {
                return UserRules.NotFound;
            }

            user.IsActive = command.IsActive;
            await db.SaveChangesAsync(ct);

            return (await UserRules.FindAsync(db, id, ct))!;
        }
    }
}
