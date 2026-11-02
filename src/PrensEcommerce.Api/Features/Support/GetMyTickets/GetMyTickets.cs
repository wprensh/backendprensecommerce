using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Support.GetMyTickets;

/// <summary>GET /api/support/tickets — casos del cliente autenticado, más recientes primero.</summary>
public static class GetMyTickets
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/support/tickets", async (ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                    TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), ct)))
                .RequireAuthorization()
                .WithTags("Support")
                .WithName("GetMyTickets");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<IReadOnlyList<TicketSummary>> HandleAsync(Guid userId, CancellationToken ct) =>
            await TicketQueries.ToSummaries(db.SupportTickets.AsNoTracking()
                    .Where(t => t.CustomerId == userId)
                    .OrderByDescending(t => t.CreatedAt), includeInternal: false)
                .ToListAsync(ct);
    }
}
