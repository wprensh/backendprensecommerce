using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Infrastructure.MultiTenancy;

namespace PrensEcommerce.Api.Features.Tenants.GetCurrentTenant;

/// <summary>GET /api/tenant — devuelve la tienda resuelta (útil para que el frontend se configure).</summary>
public static class GetCurrentTenant
{
    public sealed record Response(Guid Id, string Slug, string Name);

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/tenant", (ITenantProvider tenant) =>
                {
                    var current = tenant.Current!;
                    return TypedResults.Ok(new Response(current.Id, current.Slug, current.Name));
                })
                .WithTags("Tenants")
                .WithName("GetCurrentTenant");
    }
}
