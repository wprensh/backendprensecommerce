using System.Security.Claims;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Crm.AddCustomerNote;

/// <summary>POST /api/crm/customers/{id}/notes — nota interna del equipo sobre el cliente.</summary>
public static class AddCustomerNote
{
    public sealed record Command(string Body);

    public sealed record Response(Guid Id, string Body, string? AuthorName, DateTimeOffset CreatedAt);

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator() => RuleFor(c => c.Body).NotEmpty().MaximumLength(4000);
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("/crm/customers/{id:guid}/notes",
                    async (Guid id, Command command, ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                        (await handler.HandleAsync(id, user.GetUserId(), user.Identity?.Name, command, ct))
                            .Match(r => TypedResults.Created($"/api/crm/customers/{id}", r)))
                .WithValidation<Command>()
                .RequireCrmRead() // atención al cliente también deja notas
                .WithTags("CRM")
                .WithName("AddCrmCustomerNote");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<Response>> HandleAsync(Guid customerId, Guid authorId, string? authorName, Command command, CancellationToken ct)
        {
            if (!await db.Users.AnyAsync(u => u.Id == customerId, ct))
            {
                return Error.NotFound("Customer.NotFound", "El cliente no existe en esta tienda.");
            }

            var note = new CustomerNote { CustomerId = customerId, AuthorId = authorId, Body = command.Body.Trim() };
            db.CustomerNotes.Add(note);
            await db.SaveChangesAsync(ct);

            return new Response(note.Id, note.Body, authorName, note.CreatedAt);
        }
    }
}
