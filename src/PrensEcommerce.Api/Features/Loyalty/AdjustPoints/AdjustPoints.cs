using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Loyalty.AdjustPoints;

/// <summary>POST /api/admin/loyalty/adjustments — bonificación o corrección manual de puntos (personal).</summary>
public static class AdjustPoints
{
    public sealed record Command(Guid UserId, int Points, string Reason);

    public sealed record Response(Guid UserId, int Balance, int LifetimePoints, LoyaltyTier Tier);

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.UserId).NotEmpty();
            RuleFor(c => c.Points).NotEqual(0).InclusiveBetween(-100_000, 100_000);
            RuleFor(c => c.Reason).NotEmpty().MaximumLength(200);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("/admin/loyalty/adjustments", async (Command command, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(command, ct)).Match(r => TypedResults.Ok(r)))
                .WithValidation<Command>()
                .RequireAuthorization(AppPolicies.LoyaltyManager)
                .WithTags("Loyalty · Admin")
                .WithName("AdjustLoyaltyPoints");
    }

    public sealed class Handler(LoyaltyService loyalty, AppDbContext db) : IHandler
    {
        public async Task<Result<Response>> HandleAsync(Command command, CancellationToken ct)
        {
            if (!await db.Users.AnyAsync(u => u.Id == command.UserId, ct))
            {
                return Error.NotFound("User.NotFound", "El cliente no existe.");
            }

            var account = await loyalty.GetOrCreateAccountAsync(command.UserId, ct);
            if (account.PointsBalance + command.Points < 0)
            {
                return Error.Conflict("Loyalty.NegativeBalance", "El ajuste dejaría el saldo en negativo.");
            }

            loyalty.Adjust(account, command.Points, command.Reason.Trim());
            await db.SaveChangesAsync(ct);

            return new Response(command.UserId, account.PointsBalance, account.LifetimePoints, account.Tier);
        }
    }
}
