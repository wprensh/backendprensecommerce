namespace PrensEcommerce.Api.Domain;

// ── Esquema "sales": pedidos, líneas y pagos ──────────────────────────

public enum OrderStatus
{
    Pending = 0,
    Paid = 1,
    Shipped = 2,
    Delivered = 3,
    Cancelled = 4,
}

public enum PaymentMethod
{
    Card = 0,
    Transfer = 1,
    Cash = 2,
}

public enum PaymentStatus
{
    Pending = 0,
    Authorized = 1,
    Captured = 2,
    Failed = 3,
    Refunded = 4,
}

public sealed class Order : TenantEntity
{
    /// <summary>Número legible para el cliente (p. ej. <c>PR-20261102-7K3QZD</c>), único por tenant.</summary>
    public required string OrderNumber { get; set; }

    public Guid UserId { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public PaymentMethod PaymentMethod { get; set; }

    public required ShippingAddress ShippingAddress { get; set; }

    public decimal Subtotal { get; set; }

    public decimal ShippingCost { get; set; }

    /// <summary>Descuento aplicado con un cupón de puntos de fidelización.</summary>
    public decimal LoyaltyDiscount { get; set; }

    public decimal Total { get; set; }

    public string Currency { get; set; } = "EUR";

    public List<OrderItem> Items { get; init; } = [];

    public List<Payment> Payments { get; init; } = [];
}

public sealed class OrderItem : TenantEntity
{
    public Guid OrderId { get; set; }

    public Guid ProductId { get; set; }

    public required string ProductName { get; set; }

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }
}

/// <summary>Intento de cobro de un pedido (una pasarela puede generar varios por pedido).</summary>
public sealed class Payment : TenantEntity
{
    public Guid OrderId { get; set; }

    public PaymentMethod Method { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "EUR";

    /// <summary>Pasarela usada (ej. "stripe", "redsys", "manual").</summary>
    public string Provider { get; set; } = "manual";

    /// <summary>Id de la operación en la pasarela.</summary>
    public string? ProviderReference { get; set; }

    public string? FailureReason { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }
}

/// <summary>Value object guardado como owned type en la tabla de pedidos.</summary>
public sealed record ShippingAddress(string FullName, string Address, string City, string Phone);
