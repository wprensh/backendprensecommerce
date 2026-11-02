using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Authorization;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Users.ResetUserPassword;

/// <summary>POST /api/admin/users/{id}/password — un administrador fija una contraseña nueva.</summary>
public static class ResetUserPassword
{
    public sealed record Command(string NewPassword);

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator() => RuleFor(c => c.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(128);
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost($"{UserRules.BasePath}/{{id:guid}}/password",
                    async (Guid id, Command command, Handler handler, CancellationToken ct) =>
                        (await handler.HandleAsync(id, command, ct)).Match(_ => TypedResults.NoContent()))
                .WithValidation<Command>()
                .RequirePermission(Permissions.UsersManage)
                .WithTags(UserRules.Tag)
                .WithName("ResetUserPassword");
    }

    public sealed class Handler(AppDbContext db, IPasswordHasher<User> hasher) : IHandler
    {
        public async Task<Result<bool>> HandleAsync(Guid id, Command command, CancellationToken ct)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
            if (user is null)
            {
                return UserRules.NotFound;
            }

            user.PasswordHash = hasher.HashPassword(user, command.NewPassword);
            await db.SaveChangesAsync(ct);
            return true;
        }
    }
}
