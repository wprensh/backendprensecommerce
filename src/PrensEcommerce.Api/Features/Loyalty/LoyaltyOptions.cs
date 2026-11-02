using System.ComponentModel.DataAnnotations;
using PrensEcommerce.Api.Domain;

namespace PrensEcommerce.Api.Features.Loyalty;

/// <summary>Reglas del programa de puntos (sección <c>Loyalty</c> de appsettings).</summary>
public sealed class LoyaltyOptions
{
    public const string SectionName = "Loyalty";

    /// <summary>Puntos base por cada euro gastado (se redondea hacia abajo).</summary>
    [Range(0, 100)]
    public decimal PointsPerEuro { get; set; } = 1;

    /// <summary>Valor en euros de un punto al canjearlo (0,01 → 100 puntos = 1 €).</summary>
    [Range(0.0001, 1)]
    public decimal PointValue { get; set; } = 0.01m;

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
