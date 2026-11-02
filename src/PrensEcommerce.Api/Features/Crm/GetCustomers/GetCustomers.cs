using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Crm.GetCustomers;

/// <summary>GET /api/crm/customers?search=&amp;tag=&amp;segmentId=&amp;sort=&amp;page=&amp;pageSize= — listado de clientes con métricas.</summary>
public static class GetCustomers
{
    public sealed record Query(string? Search, string? Tag, Guid? SegmentId, string? Sort, int Page = 1, int PageSize = 20);

    public sealed record Item(
        Guid Id, string Name, string Email, IReadOnlyList<string> Tags, bool AcceptsMarketing, bool IsActive,
        DateTimeOffset RegisteredAt, int OrderCount, decimal TotalSpent, DateTimeOffset? LastOrderAt, int CartItemCount);

    public sealed record Response(IReadOnlyList<Item> Items, int Page, int PageSize, int TotalCount);

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/crm/customers", async ([AsParameters] Query query, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(query, ct)).Match(TypedResults.Ok))
                .RequireCrmRead()
                .WithTags("CRM")
                .WithName("GetCrmCustomers");
    }

    public sealed class Handler(AppDbContext db, TimeProvider clock) : IHandler
    {
        public async Task<Result<Response>> HandleAsync(Query query, CancellationToken ct)
        {
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);
            var customers = CustomerQueries.Customers(db);

            if (query.SegmentId is { } segmentId)
            {
                var segment = await db.CustomerSegments.AsNoTracking().FirstOrDefaultAsync(s => s.Id == segmentId, ct);
                if (segment is null)
                {
                    return Error.NotFound("Segment.NotFound", "El segmento no existe.");
                }

                customers = customers.Matching(segment.Rules, clock.GetUtcNow());
            }

            if (!string.IsNullOrWhiteSpace(query.Tag))
            {
                var tag = CustomerTags.Normalize(query.Tag);
                customers = customers.Where(c => c.Tags.Contains(tag));
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var pattern = $"%{query.Search.Trim()}%";
                customers = customers.Where(c => EF.Functions.ILike(c.Name, pattern) || EF.Functions.ILike(c.Email, pattern));
            }

            customers = query.Sort switch
            {
                "spent" => customers.OrderByDescending(c => c.TotalSpent).ThenBy(c => c.Name),
                "orders" => customers.OrderByDescending(c => c.OrderCount).ThenBy(c => c.Name),
                "last-order" => customers.OrderByDescending(c => c.LastOrderAt).ThenBy(c => c.Name),
                "name" => customers.OrderBy(c => c.Name),
                _ => customers.OrderByDescending(c => c.RegisteredAt),
            };

            var total = await customers.CountAsync(ct);
            var items = await customers
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new Item(c.Id, c.Name, c.Email, c.Tags, c.AcceptsMarketing, c.IsActive,
                    c.RegisteredAt, c.OrderCount, c.TotalSpent, c.LastOrderAt, c.CartItemCount))
                .ToListAsync(ct);

            return new Response(items, page, pageSize, total);
        }
    }
}
