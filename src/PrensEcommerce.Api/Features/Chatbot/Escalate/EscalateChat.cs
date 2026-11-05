using System.Security.Claims;
using System.Text;
using FluentValidation;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Features.Support.CreateTicket;

namespace PrensEcommerce.Api.Features.Chatbot.Escalate;

/// <summary>
/// POST /api/chatbot/escalate — pasa la conversación con el asistente a una persona:
/// abre un caso de soporte con la transcripción, que el equipo atiende en la bandeja de soporte.
/// </summary>
public static class EscalateChat
{
    public const int MaxLines = 40;

    public sealed record Line(bool FromBot, string Text);

    public sealed record Command(IReadOnlyList<Line> Transcript, ChatIntent? Topic);

    public sealed record Response(Guid TicketId, string Code);

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Transcript).NotNull().Must(t => t.Any(l => !l.FromBot))
                .WithMessage("La conversación debe tener al menos un mensaje del cliente.");
            RuleFor(c => c.Transcript.Count).LessThanOrEqualTo(MaxLines).When(c => c.Transcript is not null);
            RuleForEach(c => c.Transcript).ChildRules(line =>
                line.RuleFor(l => l.Text).NotEmpty().MaximumLength(2000));
            RuleFor(c => c.Topic).IsInEnum().When(c => c.Topic is not null);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("/chatbot/escalate", async (Command command, ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(user.GetUserId(), command, ct))
                        .Match(r => TypedResults.Created($"/api/support/tickets/{r.TicketId}", r)))
                .WithValidation<Command>()
                .RequireAuthorization()
                .WithTags("Chatbot")
                .WithName("EscalateChat");
    }

    public sealed class Handler(CreateTicket.Handler createTicket) : IHandler
    {
        public async Task<Result<Response>> HandleAsync(Guid userId, Command command, CancellationToken ct)
        {
            var ticket = BuildTicket(command);
            var result = await createTicket.HandleAsync(userId, ticket, ct);
            if (!result.IsSuccess)
            {
                return result.Error!;
            }

            return new Response(result.Value!.Id, result.Value.Code);
        }

        /// <summary>Asunto con la primera pregunta del cliente y la transcripción como primer mensaje del caso.</summary>
        public static CreateTicket.Command BuildTicket(Command command)
        {
            var firstQuestion = command.Transcript.First(l => !l.FromBot).Text.Trim().ReplaceLineEndings(" ");
            var subject = "Chat: " + (firstQuestion.Length > 80 ? firstQuestion[..77] + "…" : firstQuestion);

            var body = new StringBuilder("Conversación con el asistente virtual:\n\n");
            foreach (var line in command.Transcript)
            {
                body.Append(line.FromBot ? "Asistente: " : "Cliente: ").AppendLine(line.Text.Trim());
            }

            var text = body.ToString().TrimEnd();
            if (text.Length > 4000)
            {
                text = text[..3999] + "…";
            }

            return new CreateTicket.Command(subject, CategoryFor(command.Topic), text, OrderId: null);
        }

        private static TicketCategory CategoryFor(ChatIntent? topic) => topic switch
        {
            ChatIntent.OrderStatus => TicketCategory.Order,
            ChatIntent.ProductSearch => TicketCategory.Product,
            ChatIntent.Payment => TicketCategory.Payment,
            ChatIntent.Shipping => TicketCategory.Shipping,
            ChatIntent.Returns => TicketCategory.Return,
            _ => TicketCategory.Other,
        };
    }
}
