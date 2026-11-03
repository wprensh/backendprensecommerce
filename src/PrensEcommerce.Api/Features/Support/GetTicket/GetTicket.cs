using System.Security.Claims;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Security;

namespace PrensEcommerce.Api.Features.Support.GetTicket;

/// <summary>
/// GET /api/support/tickets/{id} — detalle con conversación (solo el dueño).<br/>
/// GET /api/admin/support/tickets/{id} — mismo detalle para el personal.
/// </summary>
public static class GetTicket
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("/support/tickets/{id:guid}", async (Guid id, ClaimsPrincipal user, TicketQueries queries, CancellationToken ct) =>
                    await queries.GetDetailAsync(id, user.GetUserId(), ct) is { } detail
                        ? Results.Ok(detail)
                        : Results.NotFound())
                .RequireAuthorization()
                .WithTags("Support")
                .WithName("GetTicket");

            app.MapGet("/admin/support/tickets/{id:guid}", async (Guid id, TicketQueries queries, CancellationToken ct) =>
                    await queries.GetDetailAsync(id, customerId: null, ct) is { } detail
                        ? Results.Ok(detail)
                        : Results.NotFound())
                .RequireAuthorization(AppPolicies.SupportAgent)
                .WithTags("Support · Admin")
                .WithName("GetTicketAdmin");
        }
    }
}
