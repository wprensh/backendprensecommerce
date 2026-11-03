using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Payments;

namespace PrensEcommerce.Api.Tests.Checkout;

public class SimulatedPaymentGatewayTests
{
    private readonly SimulatedPaymentGateway gateway = new(
        Options.Create(new PaymentOptions()), NullLogger<SimulatedPaymentGateway>.Instance);

    private static PaymentRequest Request(PaymentMethod method, string? token = null) =>
        new(Guid.NewGuid(), "PR-20261102-ABCDEF", 105_000m, "COP", method, token);

    [Theory]
    [InlineData("tok_sim_visa_4242")]
    [InlineData("tok_sim_mastercard_4444")]
    public async Task Card_with_valid_token_succeeds(string token)
    {
        var result = await gateway.ChargeAsync(Request(PaymentMethod.Card, token), CancellationToken.None);

        Assert.Equal(PaymentStatus.Captured, result.Status);
        Assert.StartsWith("sim_", result.ProviderReference, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("tok_sim_declined")]
    [InlineData("tok_sim_insufficient_funds")]
    [InlineData("tok_live_123")]
    [InlineData(null)]
    public async Task Card_with_test_failure_token_fails(string? token)
    {
        var result = await gateway.ChargeAsync(Request(PaymentMethod.Card, token), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.False(string.IsNullOrEmpty(result.FailureReason));
    }

    [Theory]
    [InlineData(PaymentMethod.Transfer)]
    [InlineData(PaymentMethod.Cash)]
    public async Task Offline_methods_stay_pending_with_instructions(PaymentMethod method)
    {
        var result = await gateway.ChargeAsync(Request(method), CancellationToken.None);

        Assert.Equal(PaymentStatus.Pending, result.Status);
        Assert.False(string.IsNullOrEmpty(result.Instructions));
    }
}
