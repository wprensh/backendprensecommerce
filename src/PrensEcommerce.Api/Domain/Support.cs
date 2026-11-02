namespace PrensEcommerce.Api.Domain;

// ── Esquema "support": atención al cliente ────────────────────────────

public enum TicketStatus
{
    Open = 0,
    InProgress = 1,
    WaitingCustomer = 2,
    Resolved = 3,
    Closed = 4,
}

public enum TicketPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3,
}

public enum TicketChannel
{
    Web = 0,
    Email = 1,
    Phone = 2,
    Chat = 3,
    WhatsApp = 4,
}

public sealed class SupportTicket : TenantEntity
{
    /// <summary>Cliente que abre el ticket (null si es un visitante sin cuenta).</summary>
    public Guid? CustomerId { get; set; }

    public string? ContactEmail { get; set; }

    public Guid? OrderId { get; set; }

    public required string Subject { get; set; }

    public TicketStatus Status { get; set; } = TicketStatus.Open;

    public TicketPriority Priority { get; set; } = TicketPriority.Normal;

    public TicketChannel Channel { get; set; } = TicketChannel.Web;

    /// <summary>Agente de atención asignado.</summary>
    public Guid? AssignedToId { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary>Valoración del cliente al cerrar (1–5).</summary>
    public int? SatisfactionScore { get; set; }

    public List<TicketMessage> Messages { get; init; } = [];
}

public sealed class TicketMessage : TenantEntity
{
    public Guid TicketId { get; set; }

    public Guid? AuthorId { get; set; }

    public required string Body { get; set; }

    /// <summary>Nota interna del equipo, invisible para el cliente.</summary>
    public bool IsInternal { get; set; }
}
