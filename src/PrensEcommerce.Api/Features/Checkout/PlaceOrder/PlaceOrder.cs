using System.Security.Claims;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Features.Cart;
using PrensEcommerce.Api.Features.Loyalty;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Checkout.PlaceOrder;

/// <summary>POST /api/checkout — convierte el carrito en un pedido y descuenta stock.</summary>
public static class PlaceOrder
{
    /// <param name="VoucherCode">Cupón obtenido al canjear puntos de fidelización (opcional).</param>
    public sealed record Command(ShippingAddress ShippingAddress, PaymentMethod PaymentMethod, string? VoucherCode = null);

    public sealed record Response(Guid OrderId, decimal Total, decimal LoyaltyDiscount, int PointsEarned);

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
            RuleFor(c => c.VoucherCode).MaximumLength(20);
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
                .WithName("PlaceOrder");
    }

    public sealed class Handler(CartStore carts, AppDbContext db, LoyaltyService loyalty) : IHandler
    {
        public async Task<Result<Response>> HandleAsync(Guid userId, Command command, CancellationToken ct)
        {
            var lines = await carts.GetAsync(userId, ct);
            if (lines.Count == 0)
            {
                return Error.Validation("Checkout.EmptyCart", "El carrito está vacío.");
            }

            var ids = lines.Select(l => l.ProductId).ToList();
            var products = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

            var order = new Order
            {
                UserId = userId,
                PaymentMethod = command.PaymentMethod,
                ShippingAddress = command.ShippingAddress,
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

                product.Stock -= line.Quantity;
                order.Items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitPrice = product.FinalPrice,
                    Quantity = line.Quantity,
                });
            }

            var subtotal = order.Items.Sum(i => i.UnitPrice * i.Quantity);
            if (!string.IsNullOrWhiteSpace(command.VoucherCode))
            {
                var discount = await loyalty.ApplyVoucherAsync(userId, command.VoucherCode, subtotal, ct);
                if (!discount.IsSuccess)
                {
                    return discount.Error!;
                }

                order.LoyaltyDiscount = discount.Value;
                loyalty.LinkVoucherToOrder(command.VoucherCode, order);
            }

            order.Total = subtotal - order.LoyaltyDiscount;

            // El cobro queda pendiente; la pasarela de pago lo confirmará más adelante.
            order.Payments.Add(new Payment { Method = command.PaymentMethod, Amount = order.Total });
            db.Orders.Add(order);
            var pointsEarned = await loyalty.AwardForOrderAsync(order, ct);
            await db.SaveChangesAsync(ct);
            await carts.ClearAsync(userId, ct);

            return new Response(order.Id, order.Total, order.LoyaltyDiscount, pointsEarned);
        }
    }
}
