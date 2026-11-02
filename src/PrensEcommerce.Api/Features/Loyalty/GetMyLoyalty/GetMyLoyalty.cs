using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Loyalty.GetMyLoyalty;

/// <summary>GET /api/loyalty/me — saldo, nivel, movimientos recientes y cupones activos.</summary>
public static class GetMyLoyalty
{
    public sealed record TierInfo(LoyaltyTier Tier, int MinLifetimePoints, decimal Multiplier);

    public sealed record TransactionItem(
        Guid Id, LoyaltyTransactionType Type, int Points, string Reason, Guid? OrderId, DateTimeOffset CreatedAt);

    public sealed record VoucherItem(string Code, decimal Amount, int Points, DateTimeOffset ExpiresAt);

    public sealed record Response(
        int Balance,
        int LifetimePoints,
        TierInfo Tier,
        TierInfo? NextTier,
        int PointsToNextTier,
        decimal PointValue,
        decimal BalanceValue,
        int MinRedeemPoints,
        IReadOnlyList<TransactionItem> Transactions,
        IReadOnlyList<VoucherItem> Vouchers);

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/loyalty/me", async (ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                    TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), ct)))
                .RequireAuthorization()
                .WithTags("Loyalty")
                .WithName("GetMyLoyalty");
    }

    public sealed class Handler(AppDbContext db, LoyaltyService loyalty, TimeProvider clock) : IHandler
    {
        public async Task<Response> HandleAsync(Guid userId, CancellationToken ct)
        {
            var options = loyalty.Options;
            var account = await db.LoyaltyAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == userId, ct);
            var balance = account?.PointsBalance ?? 0;
            var lifetime = account?.LifetimePoints ?? 0;

            var transactions = account is null
                ? []
                : await db.LoyaltyTransactions.AsNoTracking()
                    .Where(t => t.AccountId == account.Id)
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(50)
                    .Select(t => new TransactionItem(t.Id, t.Type, t.Points, t.Reason, t.OrderId, t.CreatedAt))
                    .ToListAsync(ct);

            var now = clock.GetUtcNow();
            var vouchers = await db.LoyaltyVouchers.AsNoTracking()
                .Where(v => v.UserId == userId && v.RedeemedAt == null && v.ExpiresAt > now)
                .OrderBy(v => v.ExpiresAt)
                .Select(v => new VoucherItem(v.Code, v.Amount, v.Points, v.ExpiresAt))
                .ToListAsync(ct);

            var tier = options.RuleFor(lifetime);
            var next = options.NextRuleFor(lifetime);

            return new Response(
                balance,
                lifetime,
                ToInfo(tier),
                next is null ? null : ToInfo(next),
                next is null ? 0 : next.MinLifetimePoints - lifetime,
                options.PointValue,
                Math.Round(balance * options.PointValue, 2),
                options.MinRedeemPoints,
                transactions,
                vouchers);
        }

        private static TierInfo ToInfo(TierRule t) => new(t.Tier, t.MinLifetimePoints, t.Multiplier);
    }
}
