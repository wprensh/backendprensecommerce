using PrensEcommerce.Api.Domain;

namespace PrensEcommerce.Api.Features.Orders.Admin;

public sealed record OrderCustomer(Guid Id, string Name, string Email);

public sealed record AdminOrderSummary(
    Guid Id,
    string OrderNumber,
    OrderStatus Status,
    PaymentMethod PaymentMethod,
    OrderCustomer Customer,
    int ItemCount,
    decimal Total,
    string Currency,
    DateTimeOffset CreatedAt);

public sealed record AdminOrderDetail(
    OrderResponse Order,
    OrderCustomer Customer,
    decimal LoyaltyDiscount,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<OrderStatus> NextStatuses);

/// <summary>Flujo de estados de un pedido. Entregado y Cancelado son finales.</summary>
public static class OrderWorkflow
{
    public const string BasePath = "/admin/orders";
    public const string Tag = "Admin · Pedidos";

    private static readonly Dictionary<OrderStatus, OrderStatus[]> Transitions = new()
    {
        [OrderStatus.Pending] = [OrderStatus.Paid, OrderStatus.Cancelled],
        [OrderStatus.Paid] = [OrderStatus.Shipped, OrderStatus.Cancelled],
        [OrderStatus.Shipped] = [OrderStatus.Delivered],
        [OrderStatus.Delivered] = [],
        [OrderStatus.Cancelled] = [],
    };

    public static IReadOnlyList<OrderStatus> Next(OrderStatus current) => Transitions[current];

    public static bool CanMove(OrderStatus from, OrderStatus to) => Transitions[from].Contains(to);

    public static string Label(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "Pendiente",
        OrderStatus.Paid => "Pagado",
        OrderStatus.Shipped => "Enviado",
        OrderStatus.Delivered => "Entregado",
        OrderStatus.Cancelled => "Cancelado",
        _ => status.ToString(),
    };
}
