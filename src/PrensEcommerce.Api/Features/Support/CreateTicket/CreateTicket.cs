using System.Security.Claims;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Support.CreateTicket;

/// <summary>POST /api/support/tickets — el cliente abre un caso (opcionalmente sobre un pedido).</summary>
public static class CreateTicket
{
    public sealed record Command(string Subject, TicketCategory Category, string Message, Guid? OrderId);

    public sealed record Response(Guid Id, string Code);

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Subject).NotEmpty().MaximumLength(200);
            RuleFor(c => c.Category).IsInEnum();
            RuleFor(c => c.Message).NotEmpty().MaximumLength(4000);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("/support/tickets", async (Command command, ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(user.GetUserId(), command, ct))
                        .Match(r => TypedResults.Created($"/api/support/tickets/{r.Id}", r)))
                .WithValidation<Command>()
                .RequireAuthorization()
                .WithTags("Support")
                .WithName("CreateTicket");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        private const int MaxAttempts = 3;

        public async Task<Result<Response>> HandleAsync(Guid userId, Command command, CancellationToken ct)
        {
            if (command.OrderId is { } orderId &&
                !await db.Orders.AnyAsync(o => o.Id == orderId && o.UserId == userId, ct))
            {
                return Error.NotFound("Order.NotFound", "El pedido no existe o no es tuyo.");
            }

            // Prioridad inicial: pagos y devoluciones se atienden antes.
            var priority = command.Category is TicketCategory.Payment or TicketCategory.Return
                ? TicketPriority.High
                : TicketPriority.Normal;

            for (var attempt = 1; ; attempt++)
            {
                // Correlativo por tenant; el índice único (tenant_id, number) resuelve la concurrencia.
                var number = (await db.SupportTickets.MaxAsync(t => (int?)t.Number, ct) ?? 0) + 1;
                var ticket = new SupportTicket
                {
                    Number = number,
                    CustomerId = userId,
                    Channel = TicketChannel.Web,
                    OrderId = command.OrderId,
                    Subject = command.Subject.Trim(),
                    Category = command.Category,
                    Priority = priority,
                };
                ticket.AddMessage(userId, fromStaff: false, command.Message.Trim());
                db.SupportTickets.Add(ticket);

                try
                {
                    await db.SaveChangesAsync(ct);
                    return new Response(ticket.Id, ticket.Code);
                }
                catch (DbUpdateException) when (attempt < MaxAttempts)
                {
                    db.ChangeTracker.Clear();
                }
            }
        }
    }
}
