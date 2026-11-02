using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Auth.Login;

/// <summary>POST /api/auth/login</summary>
public static class Login
{
    public sealed record Command(string Email, string Password);

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Email).NotEmpty().EmailAddress();
            RuleFor(c => c.Password).NotEmpty();
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("/auth/login", async (Command command, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(command, ct)).Match(TypedResults.Ok))
                .WithValidation<Command>()
                .WithTags("Auth")
                .WithName("Login");
    }

    public sealed class Handler(AppDbContext db, IPasswordHasher<User> hasher, AuthResponseFactory responses) : IHandler
    {
        private static readonly Error InvalidCredentials =
            Error.Unauthorized("Auth.InvalidCredentials", "Email o contraseña incorrectos.");

        public async Task<Result<AuthResponse>> HandleAsync(Command command, CancellationToken ct)
        {
            var email = command.Email.Trim().ToLowerInvariant();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
            if (user is null)
            {
                return InvalidCredentials;
            }

            var verification = hasher.VerifyHashedPassword(user, user.PasswordHash, command.Password);
            if (verification == PasswordVerificationResult.Failed)
            {
                return InvalidCredentials;
            }

            if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = hasher.HashPassword(user, command.Password);
                await db.SaveChangesAsync(ct);
            }

            return responses.Create(user);
        }
    }
}
