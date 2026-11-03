namespace PrensEcommerce.Api.Domain;

// ── Esquema "crm": fidelización, notas de cliente y automatizaciones ──

public enum LoyaltyTier
{
    Bronze = 0,
    Silver = 1,
    Gold = 2,
    Platinum = 3,
}

public enum LoyaltyTransactionType
{
    Earn = 0,
    Redeem = 1,
    Adjust = 2,
    Expire = 3,
}

/// <summary>Cuenta de puntos de un cliente (una por usuario).</summary>
public sealed class LoyaltyAccount : TenantEntity
{
    public Guid UserId { get; set; }

    public int PointsBalance { get; set; }

    /// <summary>Puntos ganados en total (no baja al canjear); determina el nivel.</summary>
    public int LifetimePoints { get; set; }

    public LoyaltyTier Tier { get; set; } = LoyaltyTier.Bronze;

    public List<LoyaltyTransaction> Transactions { get; init; } = [];

    /// <summary>Registra un movimiento con signo y actualiza saldo e histórico. Nunca deja saldo negativo.</summary>
    public LoyaltyTransaction Post(LoyaltyTransactionType type, int points, string reason, Guid? orderId = null)
    {
        if (PointsBalance + points < 0)
        {
            throw new InvalidOperationException("El saldo de puntos no puede ser negativo.");
        }

        PointsBalance += points;
        if (points > 0 && type is LoyaltyTransactionType.Earn or LoyaltyTransactionType.Adjust)
        {
            LifetimePoints += points;
        }

        var transaction = new LoyaltyTransaction
        {
            AccountId = Id,
            Type = type,
            Points = points,
            Reason = reason,
            OrderId = orderId,
        };
        Transactions.Add(transaction);
        return transaction;
    }
}

/// <summary>Movimiento de puntos. El saldo de la cuenta es la suma de sus movimientos.</summary>
public sealed class LoyaltyTransaction : TenantEntity
{
    public Guid AccountId { get; set; }

    public LoyaltyTransactionType Type { get; set; }

    /// <summary>Puntos con signo: positivo al ganar, negativo al canjear o expirar.</summary>
    public int Points { get; set; }

    public required string Reason { get; set; }

    public Guid? OrderId { get; set; }
}

/// <summary>Cupón generado al canjear puntos; se aplica como descuento en el checkout.</summary>
public sealed class LoyaltyVoucher : TenantEntity
{
    public Guid UserId { get; set; }

    public required string Code { get; set; }

    public decimal Amount { get; set; }

    public int Points { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RedeemedAt { get; set; }

    public Guid? OrderId { get; set; }

    public bool IsUsable(DateTimeOffset now) => RedeemedAt is null && ExpiresAt > now;
}

/// <summary>Nota del equipo sobre un cliente (historial CRM).</summary>
public sealed class CustomerNote : TenantEntity
{
    public Guid CustomerId { get; set; }

    public Guid? AuthorId { get; set; }

    public required string Body { get; set; }
}

public enum AutomationTrigger
{
    UserRegistered = 0,
    OrderPlaced = 1,
    OrderPaid = 2,
    CartAbandoned = 3,
    TicketClosed = 4,
    Birthday = 5,
    Inactivity = 6,
}

public enum AutomationAction
{
    SendEmail = 0,
    AwardPoints = 1,
    AddTag = 2,
    CreateTicket = 3,
    CallWebhook = 4,
}

public enum AutomationRunStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    Skipped = 3,
}

/// <summary>Regla "cuando ocurre X, hacer Y" configurable por cada tienda.</summary>
public sealed class AutomationRule : TenantEntity
{
    public required string Name { get; set; }

    public AutomationTrigger Trigger { get; set; }

    public AutomationAction Action { get; set; }

    /// <summary>Parámetros de la acción (plantilla, puntos, URL…). Columna jsonb.</summary>
    public string Configuration { get; set; } = "{}";

    /// <summary>Espera antes de ejecutar (ej. 60 min para carrito abandonado).</summary>
    public int DelayMinutes { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>Registro de cada ejecución de una regla, para auditoría y reintentos.</summary>
public sealed class AutomationRun : TenantEntity
{
    public Guid RuleId { get; set; }

    public AutomationRunStatus Status { get; set; } = AutomationRunStatus.Pending;

    /// <summary>Datos del evento que disparó la regla. Columna jsonb.</summary>
    public string Payload { get; set; } = "{}";

    public string? Error { get; set; }

    public DateTimeOffset? ExecutedAt { get; set; }
}
