using System.Security.Claims;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Support.RateTicket;

/// <summary>POST /api/support/tickets/{id}/rating — encuesta de satisfacción (1–5) tras resolver el caso.</summary>
public static class RateTicket
{
    public sealed record Command(int Rating, string? Comment);

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Rating).InclusiveBetween(1, 5);
            RuleFor(c => c.Comment).MaximumLength(1000);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("/support/tickets/{id:guid}/rating",
                    async (Guid id, Command command, ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                        (await handler.HandleAsync(id, user.GetUserId(), command, ct)).Match(r => TypedResults.Ok(r)))
                .WithValidation<Command>()
                .RequireAuthorization()
                .WithTags("Support")
                .WithName("RateTicket");
    }

    public sealed class Handler(AppDbContext db, TicketQueries queries) : IHandler
    {
        public async Task<Result<TicketDetail>> HandleAsync(Guid ticketId, Guid userId, Command command, CancellationToken ct)
        {
            var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == ticketId && t.CustomerId == userId, ct);
            if (ticket is null)
            {
                return Error.NotFound("Ticket.NotFound", "El caso no existe.");
            }

            if (!ticket.CanBeRated)
            {
                return Error.Conflict("Ticket.NotRateable", "Solo puedes valorar una vez un caso resuelto.");
            }

            ticket.SatisfactionScore = command.Rating;
            ticket.SatisfactionComment = string.IsNullOrWhiteSpace(command.Comment) ? null : command.Comment.Trim();
            await db.SaveChangesAsync(ct);

            return (await queries.GetDetailAsync(ticket.Id, userId, ct))!;
        }
    }
}
