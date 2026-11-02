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

public enum TicketCategory
{
    Order = 0,
    Product = 1,
    Payment = 2,
    Shipping = 3,
    Return = 4,
    Other = 5,
}

/// <summary>
/// Caso de atención al cliente. Las reglas de transición de estado viven aquí (y no en los
/// handlers) para que cliente y agentes compartan las mismas reglas.
/// </summary>
public sealed class SupportTicket : TenantEntity
{
    /// <summary>Número correlativo por tienda, visible para el cliente (TK-000123).</summary>
    public int Number { get; set; }

    public TicketCategory Category { get; set; } = TicketCategory.Other;

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

    public string? SatisfactionComment { get; set; }

    public List<TicketMessage> Messages { get; init; } = [];

    public string Code => FormatCode(Number);

    public bool IsClosed => Status == TicketStatus.Closed;

    public bool CanBeRated => Status is TicketStatus.Resolved or TicketStatus.Closed && SatisfactionScore is null;

    public static string FormatCode(int number) => $"TK-{number:D6}";

    /// <summary>
    /// Añade un mensaje. Respuesta del agente → espera al cliente; respuesta del cliente → vuelve a la cola.
    /// Las notas internas no cambian el estado.
    /// </summary>
    public TicketMessage AddMessage(Guid? authorId, bool fromStaff, string body, bool isInternal = false)
    {
        var message = new TicketMessage { TicketId = Id, AuthorId = authorId, Body = body, IsInternal = isInternal };
        Messages.Add(message);

        if (!isInternal)
        {
            Status = fromStaff ? TicketStatus.WaitingCustomer : TicketStatus.Open;
            ClosedAt = null;
        }

        return message;
    }

    public void ChangeStatus(TicketStatus status, DateTimeOffset now)
    {
        Status = status;
        ClosedAt = status is TicketStatus.Resolved or TicketStatus.Closed ? ClosedAt ?? now : null;
    }
}

public sealed class TicketMessage : TenantEntity
{
    public Guid TicketId { get; set; }

    public Guid? AuthorId { get; set; }

    public required string Body { get; set; }

    /// <summary>Nota interna del equipo, invisible para el cliente.</summary>
    public bool IsInternal { get; set; }
}
