using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Authorization;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Users.GetUsers;

/// <summary>GET /api/admin/users?search=&amp;roleId=&amp;isActive=&amp;page=&amp;pageSize=</summary>
public static class GetUsers
{
    public sealed record Query(string? Search, Guid? RoleId, bool? IsActive, int Page = 1, int PageSize = 20);

    public sealed record Response(IReadOnlyList<UserResponse> Items, int Page, int PageSize, int TotalCount);

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet(UserRules.BasePath, async ([AsParameters] Query query, Handler handler, CancellationToken ct) =>
                    TypedResults.Ok(await handler.HandleAsync(query, ct)))
                .RequirePermission(Permissions.UsersRead)
                .WithTags(UserRules.Tag)
                .WithName("GetUsers");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Response> HandleAsync(Query query, CancellationToken ct)
        {
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            var users = db.Users.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var pattern = $"%{query.Search.Trim()}%";
                users = users.Where(u => EF.Functions.ILike(u.Name, pattern) || EF.Functions.ILike(u.Email, pattern));
            }

            if (query.RoleId is { } roleId)
            {
                users = users.Where(u => u.Roles.Any(ur => ur.RoleId == roleId));
            }

            if (query.IsActive is { } isActive)
            {
                users = users.Where(u => u.IsActive == isActive);
            }

            var total = await users.CountAsync(ct);
            var items = await users
                .OrderBy(u => u.Name).ThenBy(u => u.Email)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(UserResponse.Projection)
                .ToListAsync(ct);

            return new Response(items, page, pageSize, total);
        }
    }
}
