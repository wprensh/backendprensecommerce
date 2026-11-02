using System.Security.Claims;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Support.AddTicketMessage;

/// <summary>
/// POST /api/support/tickets/{id}/messages — responde el cliente (el caso vuelve a la cola).<br/>
/// POST /api/admin/support/tickets/{id}/messages — responde un agente (queda esperando al cliente)
/// o deja una nota interna (<c>isInternal: true</c>), invisible para el cliente.
/// </summary>
public static class AddTicketMessage
{
    public sealed record Command(string Body, bool IsInternal = false);

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator() => RuleFor(c => c.Body).NotEmpty().MaximumLength(8000);
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("/support/tickets/{id:guid}/messages",
                    async (Guid id, Command command, ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                        (await handler.HandleAsync(id, user.GetUserId(), fromStaff: false, command with { IsInternal = false }, ct))
                            .Match(r => TypedResults.Ok(r)))
                .WithValidation<Command>()
                .RequireAuthorization()
                .WithTags("Support")
                .WithName("AddTicketMessage");

            app.MapPost("/admin/support/tickets/{id:guid}/messages",
                    async (Guid id, Command command, ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                        (await handler.HandleAsync(id, user.GetUserId(), fromStaff: true, command, ct))
                            .Match(r => TypedResults.Ok(r)))
                .WithValidation<Command>()
                .RequireAuthorization(AppPolicies.SupportAgent)
                .WithTags("Support · Admin")
                .WithName("ReplyTicket");
        }
    }

    public sealed class Handler(AppDbContext db, TicketQueries queries) : IHandler
    {
        public async Task<Result<TicketDetail>> HandleAsync(
            Guid ticketId, Guid authorId, bool fromStaff, Command command, CancellationToken ct)
        {
            var ticket = await db.SupportTickets
                .FirstOrDefaultAsync(t => t.Id == ticketId && (fromStaff || t.CustomerId == authorId), ct);
            if (ticket is null)
            {
                return Error.NotFound("Ticket.NotFound", "El caso no existe.");
            }

            if (ticket.IsClosed && !command.IsInternal)
            {
                return Error.Conflict("Ticket.Closed", "El caso está cerrado. Abre uno nuevo si necesitas más ayuda.");
            }

            db.TicketMessages.Add(ticket.AddMessage(authorId, fromStaff, command.Body.Trim(), command.IsInternal));
            await db.SaveChangesAsync(ct);

            return (await queries.GetDetailAsync(ticket.Id, fromStaff ? null : authorId, ct))!;
        }
    }
}
