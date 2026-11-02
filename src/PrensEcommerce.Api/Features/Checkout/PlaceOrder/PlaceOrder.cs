using System.Security.Claims;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Features.Cart;
using PrensEcommerce.Api.Infrastructure.Payments;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Checkout.PlaceOrder;

/// <summary>
/// POST /api/checkout — convierte el carrito en un pedido, cobra con la pasarela activa y descuenta stock.
/// <list type="number">
/// <item>Valida productos y stock (los precios siempre se recalculan en servidor).</item>
/// <item>Cobra con <see cref="IPaymentGateway"/>. Si la tarjeta se rechaza responde 402 y no se guarda nada.</item>
/// <item>Guarda pedido + pago y descuenta stock en una transacción; si falla, reembolsa el cobro.</item>
/// </list>
/// </summary>
public static class PlaceOrder
{
    public sealed record Line(Guid ProductId, int Quantity);

    /// <param name="Items">Líneas a pagar. Si se omite, se usa el carrito del usuario guardado en Redis.</param>
    /// <param name="PaymentToken">Token de tarjeta generado por el SDK del proveedor en el navegador.</param>
    public sealed record Command(
        ShippingAddress ShippingAddress,
        PaymentMethod PaymentMethod,
        string? PaymentToken = null,
        IReadOnlyList<Line>? Items = null);

    public sealed record PaymentSummary(PaymentStatus Status, string Provider, string? Reference, string? Instructions);

    public sealed record Response(
        Guid OrderId,
        string OrderNumber,
        OrderStatus Status,
        decimal Subtotal,
        decimal ShippingCost,
        decimal Total,
        string Currency,
        PaymentSummary Payment);

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.ShippingAddress).NotNull();
            RuleFor(c => c.ShippingAddress.FullName).NotEmpty().MaximumLength(200);
            RuleFor(c => c.ShippingAddress.Address).NotEmpty().MaximumLength(500);
            RuleFor(c => c.ShippingAddress.City).NotEmpty().MaximumLength(100);
            RuleFor(c => c.ShippingAddress.Phone).NotEmpty().MaximumLength(30);
            RuleFor(c => c.PaymentMethod).IsInEnum();
            RuleFor(c => c.PaymentToken)
                .NotEmpty().WithMessage("El pago con tarjeta requiere un token.")
                .MaximumLength(500)
                .When(c => c.PaymentMethod == PaymentMethod.Card);
            RuleFor(c => c.Items).Must(items => items!.Count <= 100).When(c => c.Items is not null);
            RuleForEach(c => c.Items).ChildRules(line =>
            {
                line.RuleFor(l => l.ProductId).NotEmpty();
                line.RuleFor(l => l.Quantity).InclusiveBetween(1, 99);
            });
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("/checkout", async (Command command, ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(user.GetUserId(), command, ct))
                        .Match(r => TypedResults.Created($"/api/orders/{r.OrderId}", r)))
                .WithValidation<Command>()
                .RequireAuthorization()
                .WithTags("Checkout")
                .WithName("PlaceOrder")
                .Produces<Response>(StatusCodes.Status201Created)
                .ProducesProblem(StatusCodes.Status402PaymentRequired)
                .ProducesProblem(StatusCodes.Status409Conflict);
    }

    public sealed class Handler(
        CartStore carts,
        AppDbContext db,
        IPaymentGateway gateway,
        IOptions<CheckoutOptions> options,
        TimeProvider clock,
        ILogger<Handler> logger) : IHandler
    {
        public async Task<Result<Response>> HandleAsync(Guid userId, Command command, CancellationToken ct)
        {
            var usesServerCart = command.Items is null;
            var lines = usesServerCart
                ? (await carts.GetAsync(userId, ct)).Select(l => new Line(l.ProductId, l.Quantity)).ToList()
                : MergeDuplicates(command.Items!);

            if (lines.Count == 0)
            {
                return Error.Validation("Checkout.EmptyCart", "El carrito está vacío.");
            }

            var ids = lines.Select(l => l.ProductId).ToList();
            var products = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

            var settings = options.Value;
            var order = new Order
            {
                OrderNumber = OrderNumber.New(clock.GetUtcNow()),
                UserId = userId,
                PaymentMethod = command.PaymentMethod,
                ShippingAddress = command.ShippingAddress,
                Currency = settings.Currency,
            };

            foreach (var line in lines)
            {
                if (!products.TryGetValue(line.ProductId, out var product))
                {
                    return Error.NotFound("Product.NotFound", "Un producto del carrito ya no existe.");
                }

                if (product.Stock < line.Quantity)
                {
                    return Error.Conflict("Checkout.InsufficientStock", $"Stock insuficiente para '{product.Name}'.");
                }

                order.Items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitPrice = product.FinalPrice,
                    Quantity = line.Quantity,
                });
            }

            order.Subtotal = order.Items.Sum(i => i.UnitPrice * i.Quantity);
            order.ShippingCost = settings.ShippingFor(order.Subtotal);
            order.Total = order.Subtotal + order.ShippingCost;

            var charge = await gateway.ChargeAsync(
                new PaymentRequest(order.Id, order.OrderNumber, order.Total, order.Currency, command.PaymentMethod, command.PaymentToken),
                ct);

            if (charge.IsFailure)
            {
                return Error.PaymentFailed("Payment.Declined", charge.FailureReason ?? "No se pudo completar el pago.");
            }

            order.Status = charge.Status == PaymentStatus.Captured ? OrderStatus.Paid : OrderStatus.Pending;
            order.Payments.Add(new Payment
            {
                Provider = gateway.Name,
                Method = command.PaymentMethod,
                Status = charge.Status,
                Amount = order.Total,
                Currency = order.Currency,
                ProviderReference = charge.ProviderReference,
                ProcessedAt = charge.Status == PaymentStatus.Captured ? clock.GetUtcNow() : null,
            });

            foreach (var item in order.Items)
            {
                products[item.ProductId].Stock -= item.Quantity;
            }

            db.Orders.Add(order);

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (charge.Status == PaymentStatus.Captured && charge.ProviderReference is not null)
            {
                // El dinero ya se cobró pero el pedido no se guardó: devolverlo antes de propagar el error.
                logger.LogError(ex, "No se pudo guardar el pedido {OrderNumber}; se reembolsa el cobro.", order.OrderNumber);
                await gateway.RefundAsync(charge.ProviderReference, order.Total, order.Currency, CancellationToken.None);
                throw;
            }

            if (usesServerCart)
            {
                await carts.ClearAsync(userId, ct);
            }

            return new Response(
                order.Id,
                order.OrderNumber,
                order.Status,
                order.Subtotal,
                order.ShippingCost,
                order.Total,
                order.Currency,
                new PaymentSummary(charge.Status, gateway.Name, charge.ProviderReference, charge.Instructions));
        }

        private static List<Line> MergeDuplicates(IReadOnlyList<Line> items) =>
            items.GroupBy(l => l.ProductId).Select(g => new Line(g.Key, g.Sum(l => l.Quantity))).ToList();
    }
}
