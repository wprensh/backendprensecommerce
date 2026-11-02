using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Loyalty;

/// <summary>
/// Reglas compartidas del programa de puntos. Lo usan los slices de Loyalty y el checkout
/// (ganar puntos y aplicar cupones). No llama a SaveChanges: el caso de uso decide la transacción.
/// </summary>
public sealed class LoyaltyService(AppDbContext db, IOptions<LoyaltyOptions> options, TimeProvider clock)
{
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public LoyaltyOptions Options => options.Value;

    /// <summary>Cuenta del usuario; si no existe se crea (y queda pendiente de guardar).</summary>
    public async Task<LoyaltyAccount> GetOrCreateAccountAsync(Guid userId, CancellationToken ct)
    {
        var account = await db.LoyaltyAccounts.FirstOrDefaultAsync(a => a.UserId == userId, ct);
        if (account is null)
        {
            account = new LoyaltyAccount { UserId = userId };
            db.LoyaltyAccounts.Add(account);
        }

        return account;
    }

    /// <summary>Puntos que corresponden a un importe según el nivel actual del cliente.</summary>
    public int PointsFor(decimal amount, int lifetimePoints)
    {
        var tier = Options.RuleFor(lifetimePoints);
        return (int)Math.Floor(Math.Max(amount, 0) * Options.PointsPerEuro * tier.Multiplier);
    }

    /// <summary>Suma los puntos de un pedido a la cuenta del cliente. Devuelve los puntos ganados.</summary>
    public async Task<int> AwardForOrderAsync(Order order, CancellationToken ct)
    {
        var account = await GetOrCreateAccountAsync(order.UserId, ct);
        var points = PointsFor(order.Total, account.LifetimePoints);
        if (points > 0)
        {
            AddTransaction(account.Post(LoyaltyTransactionType.Earn, points, "Compra online", order.Id));
            RefreshTier(account);
        }

        return points;
    }

    /// <summary>Ajuste manual del equipo (bonificación, compensación o corrección).</summary>
    public void Adjust(LoyaltyAccount account, int points, string reason)
    {
        AddTransaction(account.Post(LoyaltyTransactionType.Adjust, points, reason));
        RefreshTier(account);
    }

    /// <summary>El nivel solo sube con los puntos históricos; canjear no hace bajar de nivel.</summary>
    public void RefreshTier(LoyaltyAccount account) => account.Tier = Options.RuleFor(account.LifetimePoints).Tier;

    /// <summary>Canjea puntos por un cupón de descuento.</summary>
    public async Task<Result<LoyaltyVoucher>> RedeemAsync(Guid userId, int points, CancellationToken ct)
    {
        if (points < Options.MinRedeemPoints)
        {
            return Error.Validation("Loyalty.BelowMinimum", $"El canje mínimo es de {Options.MinRedeemPoints} puntos.");
        }

        var account = await GetOrCreateAccountAsync(userId, ct);
        if (account.PointsBalance < points)
        {
            return Error.Conflict("Loyalty.InsufficientPoints", "No tienes puntos suficientes.");
        }

        var voucher = new LoyaltyVoucher
        {
            UserId = userId,
            Code = NewCode(),
            Points = points,
            Amount = Math.Round(points * Options.PointValue, 2),
            ExpiresAt = clock.GetUtcNow().AddDays(Options.VoucherValidityDays),
        };

        AddTransaction(account.Post(LoyaltyTransactionType.Redeem, -points, $"Canje por cupón {voucher.Code}"));
        db.LoyaltyVouchers.Add(voucher);
        return voucher;
    }

    /// <summary>Valida y consume un cupón del usuario. Devuelve el descuento aplicable (sin superar el importe).</summary>
    public async Task<Result<decimal>> ApplyVoucherAsync(Guid userId, string code, decimal orderAmount, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var voucher = await db.LoyaltyVouchers.FirstOrDefaultAsync(v => v.UserId == userId && v.Code == normalized, ct);
        if (voucher is null)
        {
            return Error.NotFound("Loyalty.VoucherNotFound", "El cupón no existe.");
        }

        var now = clock.GetUtcNow();
        if (!voucher.IsUsable(now))
        {
            return Error.Conflict("Loyalty.VoucherUnavailable", "El cupón ya se usó o ha caducado.");
        }

        voucher.RedeemedAt = now;
        return Math.Min(voucher.Amount, orderAmount);
    }

    /// <summary>Asocia los cupones consumidos en esta transacción al pedido creado.</summary>
    public void LinkVoucherToOrder(string code, Order order)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var voucher = db.ChangeTracker.Entries<LoyaltyVoucher>().Select(e => e.Entity).FirstOrDefault(v => v.Code == normalized);
        if (voucher is not null)
        {
            voucher.OrderId = order.Id;
        }
    }

    // Las transacciones se crean desde el agregado; las añadimos explícitamente para que EF
    // las inserte también cuando la cuenta ya existía (colección no cargada).
    private void AddTransaction(LoyaltyTransaction transaction)
    {
        if (db.Entry(transaction).State == EntityState.Detached)
        {
            db.LoyaltyTransactions.Add(transaction);
        }
    }

    private static string NewCode() =>
        "PR-" + RandomNumberGenerator.GetString(CodeAlphabet, 8);
}
