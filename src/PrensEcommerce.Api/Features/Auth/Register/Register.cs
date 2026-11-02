using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Auth.Register;

/// <summary>POST /api/auth/register — crea un cliente en la tienda actual y devuelve su token.</summary>
public static class Register
{
    public sealed record Command(string Name, string Email, string Password);

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
            RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(320);
            RuleFor(c => c.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("/auth/register", async (Command command, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(command, ct)).Match(TypedResults.Ok))
                .WithValidation<Command>()
                .WithTags("Auth")
                .WithName("Register");
    }

    public sealed class Handler(AppDbContext db, IPasswordHasher<User> hasher, AuthResponseFactory responses) : IHandler
    {
        public async Task<Result<AuthResponse>> HandleAsync(Command command, CancellationToken ct)
        {
            var email = command.Email.Trim().ToLowerInvariant();

            // El filtro global limita la comprobación a la tienda actual: el mismo email puede existir en otra.
            if (await db.Users.AnyAsync(u => u.Email == email, ct))
            {
                return Error.Conflict("Auth.EmailInUse", "Ya existe una cuenta con ese email en esta tienda.");
            }

            var user = new User { Name = command.Name.Trim(), Email = email };
            user.PasswordHash = hasher.HashPassword(user, command.Password);

            // Todo registro público es cliente; los roles de personal se asignan desde la administración.
            var customerRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == SystemRoles.Customer, ct);
            if (customerRole is not null)
            {
                user.Roles.Add(new UserRole { RoleId = customerRole.Id });
            }

            db.Users.Add(user);
            await db.SaveChangesAsync(ct);

            return await responses.CreateAsync(user, ct);
        }
    }
}
