using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Support.UpdateTicket;

/// <summary>PATCH /api/admin/support/tickets/{id} — el personal cambia estado y/o prioridad.</summary>
public static class UpdateTicket
{
    public sealed record Command(TicketStatus? Status, TicketPriority? Priority);

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c).Must(c => c.Status is not null || c.Priority is not null)
                .WithMessage("Indica el estado o la prioridad.");
            RuleFor(c => c.Status).IsInEnum();
            RuleFor(c => c.Priority).IsInEnum();
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPatch("/admin/support/tickets/{id:guid}",
                    async (Guid id, Command command, Handler handler, CancellationToken ct) =>
                        (await handler.HandleAsync(id, command, ct)).Match(r => TypedResults.Ok(r)))
                .WithValidation<Command>()
                .RequireAuthorization(AppPolicies.SupportAgent)
                .WithTags("Support · Admin")
                .WithName("UpdateTicket");
    }

    public sealed class Handler(AppDbContext db, TicketQueries queries, TimeProvider clock) : IHandler
    {
        public async Task<Result<TicketDetail>> HandleAsync(Guid id, Command command, CancellationToken ct)
        {
            var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == id, ct);
            if (ticket is null)
            {
                return Error.NotFound("Ticket.NotFound", "El caso no existe.");
            }

            if (command.Status is { } status)
            {
                ticket.ChangeStatus(status, clock.GetUtcNow());
            }

            if (command.Priority is { } priority)
            {
                ticket.Priority = priority;
            }

            await db.SaveChangesAsync(ct);
            return (await queries.GetDetailAsync(id, customerId: null, ct))!;
        }
    }
}
