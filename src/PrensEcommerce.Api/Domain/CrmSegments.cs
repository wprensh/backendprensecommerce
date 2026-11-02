namespace PrensEcommerce.Api.Domain;

// ── Esquema "crm": eventos de cliente, segmentos y actividad de carrito ──

/// <summary>
/// Hecho de negocio de un cliente (alta, pedido, pago, ticket cerrado). Se registra en la misma
/// transacción que el cambio; el motor de automatizaciones lo procesa en segundo plano y el CRM
/// lo muestra como línea de tiempo del cliente.
/// </summary>
public sealed class CustomerEvent : TenantEntity
{
    public Guid UserId { get; set; }

    public AutomationTrigger Type { get; set; }

    /// <summary>Entidad relacionada (pedido, ticket…).</summary>
    public Guid? ReferenceId { get; set; }

    public decimal? Amount { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }
}

/// <summary>
/// Última actividad del carrito de un cliente (el carrito vive en Redis). Sirve para detectar
/// carritos abandonados sin recorrer Redis.
/// </summary>
public sealed class CartActivity : TenantEntity
{
    public Guid UserId { get; set; }

    /// <summary>Unidades en el carrito tras el último cambio (0 = vacío o comprado).</summary>
    public int ItemCount { get; set; }

    public DateTimeOffset LastActivityAt { get; set; }
}

/// <summary>Segmento dinámico de clientes: guarda reglas, no miembros; se evalúa en cada consulta.</summary>
public sealed class CustomerSegment : TenantEntity
{
    public required string Name { get; set; }

    public string Description { get; set; } = string.Empty;

    public SegmentRules Rules { get; set; } = new();
}

/// <summary>Criterios del segmento; todos opcionales y combinados con AND.</summary>
public sealed class SegmentRules
{
    public int? MinOrders { get; set; }

    public int? MaxOrders { get; set; }

    public decimal? MinTotalSpent { get; set; }

    /// <summary>Último pedido hace al menos N días (clientes dormidos).</summary>
    public int? LastOrderOlderThanDays { get; set; }

    /// <summary>Último pedido en los últimos N días (clientes activos).</summary>
    public int? LastOrderWithinDays { get; set; }

    /// <summary>Registrados en los últimos N días.</summary>
    public int? RegisteredWithinDays { get; set; }

    /// <summary>Con productos en el carrito ahora mismo.</summary>
    public bool? HasCartItems { get; set; }

    /// <summary>Solo clientes que aceptan comunicaciones de marketing.</summary>
    public bool? AcceptsMarketing { get; set; }

    public string? Tag { get; set; }
}
