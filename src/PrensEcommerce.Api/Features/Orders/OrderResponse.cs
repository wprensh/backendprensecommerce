using System.Linq.Expressions;
using PrensEcommerce.Api.Domain;

namespace PrensEcommerce.Api.Features.Orders;

public sealed record OrderItemResponse(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal Subtotal);

public sealed record PaymentResponse(
    string Provider,
    PaymentMethod Method,
    PaymentStatus Status,
    decimal Amount,
    string Currency,
    string? Reference,
    string? FailureReason,
    DateTimeOffset CreatedAt);

public sealed record OrderResponse(
    Guid Id,
    string OrderNumber,
    OrderStatus Status,
    PaymentMethod PaymentMethod,
    ShippingAddress ShippingAddress,
    IReadOnlyList<OrderItemResponse> Items,
    decimal Subtotal,
    decimal ShippingCost,
    decimal Total,
    string Currency,
    PaymentResponse? Payment,
    DateTimeOffset CreatedAt)
{
    /// <summary>Proyección traducible a SQL; el pago mostrado es el último intento.</summary>
    public static readonly Expression<Func<Order, OrderResponse>> Projection = o => new OrderResponse(
        o.Id,
        o.OrderNumber,
        o.Status,
        o.PaymentMethod,
        o.ShippingAddress,
        o.Items.Select(i => new OrderItemResponse(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity, i.UnitPrice * i.Quantity)).ToList(),
        o.Subtotal,
        o.ShippingCost,
        o.Total,
        o.Currency,
        o.Payments
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PaymentResponse(p.Provider, p.Method, p.Status, p.Amount, p.Currency, p.ProviderReference, p.FailureReason, p.CreatedAt))
            .FirstOrDefault(),
        o.CreatedAt);
}
