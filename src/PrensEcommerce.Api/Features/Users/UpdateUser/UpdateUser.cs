using System.Security.Claims;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Authorization;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Users.UpdateUser;

/// <summary>PUT /api/admin/users/{id} — datos básicos y roles (sustituye la lista completa de roles).</summary>
public static class UpdateUser
{
    public sealed record Command(string Name, string Email, IReadOnlyList<Guid> RoleIds);

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
            RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(320);
            RuleFor(c => c.RoleIds).NotNull().NotEmpty().WithMessage("Asigna al menos un rol.");
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPut($"{UserRules.BasePath}/{{id:guid}}",
                    async (Guid id, Command command, Handler handler, ClaimsPrincipal principal, CancellationToken ct) =>
                        (await handler.HandleAsync(id, command, principal.GetUserId(), ct)).Match(TypedResults.Ok))
                .WithValidation<Command>()
                .RequirePermission(Permissions.UsersManage)
                .WithTags(UserRules.Tag)
                .WithName("UpdateUser");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<UserResponse>> HandleAsync(Guid id, Command command, Guid currentUserId, CancellationToken ct)
        {
            var user = await db.Users.Include(u => u.Roles).ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == id, ct);
            if (user is null)
            {
                return UserRules.NotFound;
            }

            var email = UserRules.NormalizeEmail(command.Email);
            if (email != user.Email && await db.Users.AnyAsync(u => u.Email == email && u.Id != id, ct))
            {
                return Error.Conflict("Users.EmailInUse", "Ya existe un usuario con ese email en esta tienda.");
            }

            if (await UserRules.ValidateRolesAsync(db, command.RoleIds, ct) is { } roleError)
            {
                return roleError;
            }

            var newRoleIds = command.RoleIds.ToHashSet();

            // Evita quedarse fuera: nadie puede quitarse a sí mismo el rol Admin.
            var adminRole = user.Roles.Select(ur => ur.Role).FirstOrDefault(r => r is { IsSystem: true, Name: SystemRoles.Admin });
            if (id == currentUserId && adminRole is not null && !newRoleIds.Contains(adminRole.Id))
            {
                return Error.Validation("Users.CannotRemoveOwnAdmin", "No puedes quitarte a ti mismo el rol Admin.");
            }

            user.Name = command.Name.Trim();
            user.Email = email;

            foreach (var removed in user.Roles.Where(ur => !newRoleIds.Contains(ur.RoleId)).ToList())
            {
                db.UserRoles.Remove(removed);
            }

            // Add explícito: la entidad nace con Id (Guid v7) y, descubierta solo por navegación,
            // EF la trataría como existente (Modified) en lugar de nueva.
            foreach (var roleId in newRoleIds.Where(r => user.Roles.All(ur => ur.RoleId != r)).ToList())
            {
                db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roleId });
            }

            await db.SaveChangesAsync(ct);
            return (await UserRules.FindAsync(db, id, ct))!;
        }
    }
}
