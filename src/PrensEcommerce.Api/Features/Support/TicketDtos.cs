using PrensEcommerce.Api.Domain;

namespace PrensEcommerce.Api.Features.Support;

public sealed record TicketSummary(
    Guid Id,
    string Code,
    string Subject,
    TicketCategory Category,
    TicketPriority Priority,
    TicketStatus Status,
    TicketChannel Channel,
    Guid? OrderId,
    int MessageCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record TicketMessageItem(
    Guid Id, bool IsStaff, bool IsInternal, string AuthorName, string Body, DateTimeOffset CreatedAt);

public sealed record TicketDetail(
    Guid Id,
    string Code,
    string Subject,
    TicketCategory Category,
    TicketPriority Priority,
    TicketStatus Status,
    TicketChannel Channel,
    Guid? OrderId,
    Guid? CustomerId,
    string CustomerName,
    string? CustomerEmail,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt,
    bool CanBeRated,
    int? SatisfactionScore,
    string? SatisfactionComment,
    IReadOnlyList<TicketMessageItem> Messages);
