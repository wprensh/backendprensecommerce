using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Support;

/// <summary>Lecturas compartidas por los slices de soporte (cliente y agentes).</summary>
public sealed class TicketQueries(AppDbContext db)
{
    /// <param name="customerId">Si se indica, solo devuelve el caso si es de ese cliente y oculta las notas internas.</param>
    public async Task<TicketDetail?> GetDetailAsync(Guid ticketId, Guid? customerId, CancellationToken ct)
    {
        var forCustomer = customerId is not null;
        var ticket = await db.SupportTickets.AsNoTracking()
            .Include(t => t.Messages.Where(m => !forCustomer || !m.IsInternal).OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(t => t.Id == ticketId && (!forCustomer || t.CustomerId == customerId), ct);
        if (ticket is null)
        {
            return null;
        }

        var userIds = ticket.Messages
            .Select(m => m.AuthorId)
            .Append(ticket.CustomerId)
            .OfType<Guid>()
            .Distinct()
            .ToList();
        var users = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.Name, u.Email }, ct);

        var customer = ticket.CustomerId is { } id ? users.GetValueOrDefault(id) : null;
        return new TicketDetail(
            ticket.Id,
            ticket.Code,
            ticket.Subject,
            ticket.Category,
            ticket.Priority,
            ticket.Status,
            ticket.Channel,
            ticket.OrderId,
            ticket.CustomerId,
            customer?.Name ?? "Visitante",
            customer?.Email ?? ticket.ContactEmail,
            ticket.CreatedAt,
            ticket.ClosedAt,
            ticket.CanBeRated,
            ticket.SatisfactionScore,
            ticket.SatisfactionComment,
            ticket.Messages
                .Select(m =>
                {
                    // Un mensaje es del equipo si no lo escribió el cliente del caso.
                    var isStaff = m.AuthorId != ticket.CustomerId;
                    var author = m.AuthorId is { } a ? users.GetValueOrDefault(a)?.Name : null;
                    return new TicketMessageItem(
                        m.Id,
                        isStaff,
                        m.IsInternal,
                        isStaff && forCustomer ? "Atención al cliente" : author ?? (isStaff ? "Atención al cliente" : "Cliente"),
                        m.Body,
                        m.CreatedAt);
                })
                .ToList());
    }

    /// <param name="includeInternal">Cuenta también las notas internas (vista de agentes).</param>
    public static IQueryable<TicketSummary> ToSummaries(IQueryable<SupportTicket> query, bool includeInternal) =>
        query.Select(t => new TicketSummary(
            t.Id,
            SupportTicket.FormatCode(t.Number),
            t.Subject,
            t.Category,
            t.Priority,
            t.Status,
            t.Channel,
            t.OrderId,
            t.Messages.Count(m => includeInternal || !m.IsInternal),
            t.CreatedAt,
            t.UpdatedAt));
}
