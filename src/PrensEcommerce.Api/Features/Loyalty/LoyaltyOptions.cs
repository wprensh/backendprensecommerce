using System.ComponentModel.DataAnnotations;
using PrensEcommerce.Api.Domain;

namespace PrensEcommerce.Api.Features.Loyalty;

/// <summary>Reglas del programa de puntos (sección <c>Loyalty</c> de appsettings).</summary>
public sealed class LoyaltyOptions
{
    public const string SectionName = "Loyalty";

    /// <summary>Pesos (COP) que hay que gastar para ganar 1 punto base (se redondea hacia abajo).</summary>
    [Range(1, 1_000_000)]
    public decimal PesosPerPoint { get; set; } = 1000;

    /// <summary>Valor en pesos de un punto al canjearlo ($10 → 1 % de devolución con 1 punto/$1.000).</summary>
    [Range(0.01, 100_000)]
    public decimal PointValue { get; set; } = 10;

    /// <summary>Mínimo de puntos por canje.</summary>
    [Range(1, int.MaxValue)]
    public int MinRedeemPoints { get; set; } = 500;

    [Range(1, 3650)]
    public int VoucherValidityDays { get; set; } = 90;

    /// <summary>Niveles por puntos acumulados históricamente.</summary>
    public List<TierRule> Tiers { get; set; } =
    [
        new() { Tier = LoyaltyTier.Bronze, MinLifetimePoints = 0, Multiplier = 1m },
        new() { Tier = LoyaltyTier.Silver, MinLifetimePoints = 1000, Multiplier = 1.25m },
        new() { Tier = LoyaltyTier.Gold, MinLifetimePoints = 5000, Multiplier = 1.5m },
        new() { Tier = LoyaltyTier.Platinum, MinLifetimePoints = 15000, Multiplier = 2m },
    ];

    public TierRule RuleFor(int lifetimePoints) =>
        Tiers.OrderBy(t => t.MinLifetimePoints).LastOrDefault(t => lifetimePoints >= t.MinLifetimePoints)
        ?? new TierRule { Tier = LoyaltyTier.Bronze, MinLifetimePoints = 0, Multiplier = 1m };

    public TierRule? NextRuleFor(int lifetimePoints) =>
        Tiers.OrderBy(t => t.MinLifetimePoints).FirstOrDefault(t => t.MinLifetimePoints > lifetimePoints);
}

public sealed class TierRule
{
    public LoyaltyTier Tier { get; set; }

    public int MinLifetimePoints { get; set; }

    /// <summary>Multiplicador de puntos ganados por compra en este nivel.</summary>
    public decimal Multiplier { get; set; } = 1m;
}
