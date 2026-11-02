using System.Text.RegularExpressions;
using PrensEcommerce.Api.Features.Checkout;

namespace PrensEcommerce.Api.Tests.Checkout;

public partial class CheckoutRulesTests
{
    [Theory]
    [InlineData(null, 10.0, 5.0)]
    [InlineData(50.0, 49.99, 5.0)]
    [InlineData(50.0, 50.0, 0.0)]
    [InlineData(50.0, 120.0, 0.0)]
    public void ShippingFor_applies_free_shipping_threshold(double? threshold, double subtotal, double expected)
    {
        var options = new CheckoutOptions { ShippingCost = 5m, FreeShippingFrom = (decimal?)threshold };

        Assert.Equal((decimal)expected, options.ShippingFor((decimal)subtotal));
    }

    [Fact]
    public void OrderNumber_has_readable_format()
    {
        var number = OrderNumber.New(new DateTimeOffset(2026, 11, 2, 18, 0, 0, TimeSpan.Zero));

        Assert.Matches(Format(), number);
        Assert.StartsWith("PR-20261102-", number, StringComparison.Ordinal);
    }

    [GeneratedRegex("^PR-[0-9]{8}-[2-9A-HJ-NP-Z]{6}$")]
    private static partial Regex Format();
}
