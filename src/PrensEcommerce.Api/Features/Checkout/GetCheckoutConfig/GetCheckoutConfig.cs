using Microsoft.Extensions.Options;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Payments;

namespace PrensEcommerce.Api.Features.Checkout.GetCheckoutConfig;

/// <summary>GET /api/checkout/config — lo que la página de pago necesita saber antes de pintar.</summary>
public static class GetCheckoutConfig
{
    public sealed record Response(
        string Currency,
        decimal ShippingCost,
        decimal? FreeShippingFrom,
        string PaymentProvider,
        IReadOnlyList<PaymentMethod> PaymentMethods);

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/checkout/config", (Handler handler) => TypedResults.Ok(handler.Handle()))
                .WithTags("Checkout")
                .WithName("GetCheckoutConfig");
    }

    public sealed class Handler(IOptions<CheckoutOptions> checkout, IPaymentGateway gateway) : IHandler
    {
        public Response Handle() => new(
            checkout.Value.Currency,
            checkout.Value.ShippingCost,
            checkout.Value.FreeShippingFrom,
            gateway.Name,
            Enum.GetValues<PaymentMethod>());
    }
}
