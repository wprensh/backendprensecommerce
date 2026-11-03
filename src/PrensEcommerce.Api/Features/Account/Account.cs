using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Account;

/// <summary>Datos de la cuenta que el propio cliente puede ver y editar en «Mi cuenta».</summary>
public sealed record ProfileResponse(
    Guid Id, string Name, string Email, string? Phone, bool AcceptsMarketing, DateTimeOffset CreatedAt);

/// <summary>GET /api/me — perfil del usuario autenticado.</summary>
public static class GetProfile
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/me", async (ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(user.GetUserId(), ct)).Match(TypedResults.Ok))
                .RequireAuthorization()
                .WithTags("Account")
                .WithName("GetProfile");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<ProfileResponse>> HandleAsync(Guid userId, CancellationToken ct)
        {
            var profile = await db.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new ProfileResponse(u.Id, u.Name, u.Email, u.Phone, u.AcceptsMarketing, u.CreatedAt))
                .FirstOrDefaultAsync(ct);

            return profile is null ? Error.NotFound("Account.NotFound", "La cuenta no existe.") : profile;
        }
    }
}

/// <summary>PUT /api/me — actualiza nombre, teléfono y consentimiento de marketing. El email no se cambia aquí.</summary>
public static class UpdateProfile
{
    public sealed record Command(string Name, string? Phone, bool AcceptsMarketing);

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
            RuleFor(c => c.Phone).MaximumLength(30).Matches(@"^[0-9+()\s-]*$")
                .WithMessage("El teléfono solo puede tener números, espacios y los signos + ( ) -.");
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPut("/me", async (Command command, ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(user.GetUserId(), command, ct)).Match(TypedResults.Ok))
                .RequireAuthorization()
                .WithValidation<Command>()
                .WithTags("Account")
                .WithName("UpdateProfile");
    }

    public sealed class Handler(AppDbContext db, GetProfile.Handler profile) : IHandler
    {
        public async Task<Result<ProfileResponse>> HandleAsync(Guid userId, Command command, CancellationToken ct)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user is null)
            {
                return Error.NotFound("Account.NotFound", "La cuenta no existe.");
            }

            user.Name = command.Name.Trim();
            user.Phone = string.IsNullOrWhiteSpace(command.Phone) ? null : command.Phone.Trim();
            user.AcceptsMarketing = command.AcceptsMarketing;
            await db.SaveChangesAsync(ct);

            return await profile.HandleAsync(userId, ct);
        }
    }
}

/// <summary>POST /api/me/password — cambia la contraseña comprobando la actual.</summary>
public static class ChangePassword
{
    public sealed record Command(string CurrentPassword, string NewPassword);

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.CurrentPassword).NotEmpty();
            RuleFor(c => c.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(128)
                .NotEqual(c => c.CurrentPassword).WithMessage("La nueva contraseña debe ser distinta de la actual.");
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("/me/password", async (Command command, ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(user.GetUserId(), command, ct)).Match(_ => TypedResults.NoContent()))
                .RequireAuthorization()
                .WithValidation<Command>()
                .WithTags("Account")
                .WithName("ChangePassword");
    }

    public sealed class Handler(AppDbContext db, IPasswordHasher<User> hasher) : IHandler
    {
        public async Task<Result<bool>> HandleAsync(Guid userId, Command command, CancellationToken ct)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user is null)
            {
                return Error.NotFound("Account.NotFound", "La cuenta no existe.");
            }

            if (hasher.VerifyHashedPassword(user, user.PasswordHash, command.CurrentPassword) == PasswordVerificationResult.Failed)
            {
                return Error.Validation("Account.WrongPassword", "La contraseña actual no es correcta.");
            }

            user.PasswordHash = hasher.HashPassword(user, command.NewPassword);
            await db.SaveChangesAsync(ct);
            return true;
        }
    }
}
