using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Authorization;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Users.GetUserById;

/// <summary>GET /api/admin/users/{id}</summary>
public static class GetUserById
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet($"{UserRules.BasePath}/{{id:guid}}", async (Guid id, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(id, ct)).Match(TypedResults.Ok))
                .RequirePermission(Permissions.UsersRead)
                .WithTags(UserRules.Tag)
                .WithName("GetUserById");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<UserResponse>> HandleAsync(Guid id, CancellationToken ct) =>
            await UserRules.FindAsync(db, id, ct) is { } user ? user : UserRules.NotFound;
    }
}
