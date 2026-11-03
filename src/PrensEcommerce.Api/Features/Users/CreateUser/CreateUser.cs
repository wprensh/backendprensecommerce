using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Authorization;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Users.CreateUser;

/// <summary>POST /api/admin/users — alta de un usuario (personal o cliente) con sus roles.</summary>
public static class CreateUser
{
    public sealed record Command(string Name, string Email, string Password, IReadOnlyList<Guid> RoleIds, bool IsActive = true);

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
            RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(320);
            RuleFor(c => c.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
            RuleFor(c => c.RoleIds).NotNull().NotEmpty().WithMessage("Asigna al menos un rol.");
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost(UserRules.BasePath, async (Command command, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(command, ct))
                    .Match(user => TypedResults.Created($"/api{UserRules.BasePath}/{user.Id}", user)))
                .WithValidation<Command>()
                .RequirePermission(Permissions.UsersManage)
                .WithTags(UserRules.Tag)
                .WithName("CreateUser");
    }

    public sealed class Handler(AppDbContext db, IPasswordHasher<User> hasher) : IHandler
    {
        public async Task<Result<UserResponse>> HandleAsync(Command command, CancellationToken ct)
        {
            var email = UserRules.NormalizeEmail(command.Email);
            if (await db.Users.AnyAsync(u => u.Email == email, ct))
            {
                return Error.Conflict("Users.EmailInUse", "Ya existe un usuario con ese email en esta tienda.");
            }

            if (await UserRules.ValidateRolesAsync(db, command.RoleIds, ct) is { } roleError)
            {
                return roleError;
            }

            var user = new User { Name = command.Name.Trim(), Email = email, IsActive = command.IsActive };
            user.PasswordHash = hasher.HashPassword(user, command.Password);
            user.Roles.AddRange(command.RoleIds.Distinct().Select(roleId => new UserRole { RoleId = roleId }));

            db.Users.Add(user);
            await db.SaveChangesAsync(ct);

            return (await UserRules.FindAsync(db, user.Id, ct))!;
        }
    }
}
