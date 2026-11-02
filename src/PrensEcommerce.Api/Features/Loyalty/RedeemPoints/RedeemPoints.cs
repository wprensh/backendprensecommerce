using System.Security.Claims;
using FluentValidation;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Loyalty.RedeemPoints;

/// <summary>POST /api/loyalty/redeem — canjea puntos por un cupón de descuento para el checkout.</summary>
public static class RedeemPoints
{
    public sealed record Command(int Points);

    public sealed record Response(string Code, decimal Amount, int Points, DateTimeOffset ExpiresAt);

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator() => RuleFor(c => c.Points).GreaterThan(0);
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("/loyalty/redeem", async (Command command, ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(user.GetUserId(), command, ct)).Match(r => TypedResults.Ok(r)))
                .WithValidation<Command>()
                .RequireAuthorization()
                .WithTags("Loyalty")
                .WithName("RedeemPoints");
    }

    public sealed class Handler(LoyaltyService loyalty, AppDbContext db) : IHandler
    {
        public async Task<Result<Response>> HandleAsync(Guid userId, Command command, CancellationToken ct)
        {
            var result = await loyalty.RedeemAsync(userId, command.Points, ct);
            if (!result.IsSuccess)
            {
                return result.Error!;
            }

            await db.SaveChangesAsync(ct);
            var v = result.Value!;
            return new Response(v.Code, v.Amount, v.Points, v.ExpiresAt);
        }
    }
}
