using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Features.Loyalty;

namespace PrensEcommerce.Api.Tests.Loyalty;

public class LoyaltyTests
{
    [Fact]
    public void Earning_adds_to_balance_and_lifetime_but_redeeming_only_lowers_balance()
    {
        var account = new LoyaltyAccount { UserId = Guid.NewGuid() };

        account.Post(LoyaltyTransactionType.Earn, 800, "Compra");
        account.Post(LoyaltyTransactionType.Redeem, -500, "Cupón");

        Assert.Equal(300, account.PointsBalance);
        Assert.Equal(800, account.LifetimePoints);
        Assert.Equal(2, account.Transactions.Count);
        Assert.All(account.Transactions, t => Assert.Equal(account.Id, t.AccountId));
    }

    [Fact]
    public void Balance_can_not_go_negative()
    {
        var account = new LoyaltyAccount { UserId = Guid.NewGuid(), PointsBalance = 100 };

        Assert.Throws<InvalidOperationException>(() =>
            account.Post(LoyaltyTransactionType.Adjust, -101, "Corrección"));
    }

    [Theory]
    [InlineData(0, LoyaltyTier.Bronze, LoyaltyTier.Silver)]
    [InlineData(999, LoyaltyTier.Bronze, LoyaltyTier.Silver)]
    [InlineData(1000, LoyaltyTier.Silver, LoyaltyTier.Gold)]
    [InlineData(7000, LoyaltyTier.Gold, LoyaltyTier.Platinum)]
    [InlineData(20000, LoyaltyTier.Platinum, null)]
    public void Tier_depends_on_lifetime_points(int lifetime, LoyaltyTier tier, LoyaltyTier? next)
    {
        var options = new LoyaltyOptions();

        Assert.Equal(tier, options.RuleFor(lifetime).Tier);
        Assert.Equal(next, options.NextRuleFor(lifetime)?.Tier);
    }

    [Fact]
    public void Voucher_is_usable_until_redeemed_or_expired()
    {
        var now = DateTimeOffset.UtcNow;
        var voucher = new LoyaltyVoucher { UserId = Guid.NewGuid(), Code = "PR-TEST", ExpiresAt = now.AddDays(1) };

        Assert.True(voucher.IsUsable(now));
        Assert.False(voucher.IsUsable(now.AddDays(2)));

        voucher.RedeemedAt = now;
        Assert.False(voucher.IsUsable(now));
    }

    [Theory]
    [InlineData(579_900, 0, 579)]       // Bronce: 1 punto por cada $1.000
    [InlineData(579_900, 1_000, 724)]   // Plata: ×1,25
    [InlineData(999, 0, 0)]
    public void Points_are_earned_per_thousand_pesos(decimal amount, int lifetime, int expected)
    {
        var service = new LoyaltyService(null!, Microsoft.Extensions.Options.Options.Create(new LoyaltyOptions()), TimeProvider.System);

        Assert.Equal(expected, service.PointsFor(amount, lifetime));
    }

    [Fact]
    public void Default_redemption_is_one_percent_in_pesos()
    {
        var options = new LoyaltyOptions();

        Assert.Equal(5_000m, options.MinRedeemPoints * options.PointValue);
    }
}
