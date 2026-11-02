namespace PrensEcommerce.Api.Domain;

public enum OrderStatus
{
    Pending = 0,
    Paid = 1,
    Shipped = 2,
    Cancelled = 3,
}

public enum PaymentMethod
{
    Card = 0,
    Transfer = 1,
    Cash = 2,
}

public sealed class Order : TenantEntity
{
    public Guid UserId { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public PaymentMethod PaymentMethod { get; set; }

    public required ShippingAddress ShippingAddress { get; set; }

    public decimal Total { get; set; }

    public List<OrderItem> Items { get; init; } = [];
}

public sealed class OrderItem : TenantEntity
{
    public Guid OrderId { get; set; }

    public Guid ProductId { get; set; }

    public required string ProductName { get; set; }

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }
}

/// <summary>Value object guardado como owned type en la tabla de pedidos.</summary>
public sealed record ShippingAddress(string FullName, string Address, string City, string Phone);
