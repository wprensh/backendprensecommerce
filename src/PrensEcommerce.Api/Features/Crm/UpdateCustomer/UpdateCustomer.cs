using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Crm.UpdateCustomer;

/// <summary>PUT /api/crm/customers/{id} — etiquetas y consentimiento de marketing del cliente.</summary>
public static class UpdateCustomer
{
    public sealed record Command(IReadOnlyList<string> Tags, bool AcceptsMarketing);

    public sealed record Response(Guid Id, IReadOnlyList<string> Tags, bool AcceptsMarketing);

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Tags).NotNull().Must(t => t.Count <= 20).WithMessage("Máximo 20 etiquetas.");
            RuleForEach(c => c.Tags).NotEmpty().MaximumLength(40);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPut("/crm/customers/{id:guid}", async (Guid id, Command command, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(id, command, ct)).Match(TypedResults.Ok))
                .WithValidation<Command>()
                .RequireCrmWrite()
                .WithTags("CRM")
                .WithName("UpdateCrmCustomer");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<Response>> HandleAsync(Guid id, Command command, CancellationToken ct)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
            if (user is null)
            {
                return Error.NotFound("Customer.NotFound", "El cliente no existe en esta tienda.");
            }

            user.Tags = CustomerTags.Normalize(command.Tags);
            user.AcceptsMarketing = command.AcceptsMarketing;
            await db.SaveChangesAsync(ct);

            return new Response(user.Id, user.Tags, user.AcceptsMarketing);
        }
    }
}
