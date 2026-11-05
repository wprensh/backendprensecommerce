using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Campaigns;

/// <summary>Consulta compartida: la campaña que se muestra ahora en el banner del catálogo.</summary>
public static class LiveCampaign
{
    public static IQueryable<Campaign> Live(this IQueryable<Campaign> campaigns, DateTimeOffset now) =>
        campaigns
            .Where(c => c.IsActive && (c.StartsAt == null || c.StartsAt <= now) && (c.EndsAt == null || c.EndsAt > now))
            .OrderByDescending(c => c.Priority)
            .ThenByDescending(c => c.StartsAt ?? c.CreatedAt)
            .ThenByDescending(c => c.CreatedAt);
}

/// <summary>GET /api/campaigns/active — banner vigente del catálogo (público). 204 si no hay ninguna.</summary>
public static class GetActiveCampaign
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/campaigns/active", async (Handler handler, CancellationToken ct) =>
                    await handler.HandleAsync(ct) is { } banner ? Results.Ok(banner) : Results.NoContent())
                .AllowAnonymous()
                .WithTags("Campaigns")
                .WithName("GetActiveCampaign")
                .Produces<CampaignBannerResponse>()
                .Produces(StatusCodes.Status204NoContent);
    }

    public sealed class Handler(AppDbContext db, TimeProvider clock) : IHandler
    {
        public async Task<CampaignBannerResponse?> HandleAsync(CancellationToken ct)
        {
            var campaign = await db.Campaigns.AsNoTracking().Live(clock.GetUtcNow()).FirstOrDefaultAsync(ct);
            return campaign?.ToBanner();
        }
    }
}

/// <summary>GET /api/admin/campaigns — todas las campañas, con su estado y cuál se está mostrando.</summary>
public static class GetCampaigns
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/admin/campaigns", async (Handler handler, CancellationToken ct) =>
                    TypedResults.Ok(await handler.HandleAsync(ct)))
                .RequireAuthorization(AppPolicies.CampaignManager)
                .WithTags("Campaigns")
                .WithName("GetCampaigns");
    }

    public sealed class Handler(AppDbContext db, TimeProvider clock) : IHandler
    {
        public async Task<IReadOnlyList<CampaignResponse>> HandleAsync(CancellationToken ct)
        {
            var now = clock.GetUtcNow();
            var showingId = await db.Campaigns.Live(now).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct);
            var campaigns = await db.Campaigns.AsNoTracking()
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync(ct);

            // Primero la que se muestra, luego vigentes, programadas, inactivas y terminadas.
            return campaigns
                .Select(c => c.ToResponse(now, showingId))
                .OrderByDescending(c => c.IsShowing)
                .ThenBy(c => c.Status switch
                {
                    CampaignStatus.Running => 0,
                    CampaignStatus.Scheduled => 1,
                    CampaignStatus.Inactive => 2,
                    _ => 3,
                })
                .ThenByDescending(c => c.Priority)
                .ToList();
        }
    }
}

/// <summary>GET /api/admin/campaigns/{id}</summary>
public static class GetCampaignById
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/admin/campaigns/{id:guid}", async (Guid id, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(id, ct)).Match(TypedResults.Ok))
                .RequireAuthorization(AppPolicies.CampaignManager)
                .WithTags("Campaigns")
                .WithName("GetCampaignById")
                .Produces<CampaignResponse>()
                .ProducesProblem(StatusCodes.Status404NotFound);
    }

    public sealed class Handler(AppDbContext db, TimeProvider clock) : IHandler
    {
        public async Task<Result<CampaignResponse>> HandleAsync(Guid id, CancellationToken ct)
        {
            var campaign = await db.Campaigns.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
            if (campaign is null)
            {
                return Error.NotFound("Campaign.NotFound", $"No existe la campaña {id}.");
            }

            var now = clock.GetUtcNow();
            var showingId = await db.Campaigns.Live(now).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct);
            return campaign.ToResponse(now, showingId);
        }
    }
}

/// <summary>POST /api/admin/campaigns</summary>
public static class CreateCampaign
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("/admin/campaigns", async (CampaignCommand command, Handler handler, CancellationToken ct) =>
                {
                    var campaign = await handler.HandleAsync(command, ct);
                    return TypedResults.Created($"/api/admin/campaigns/{campaign.Id}", campaign);
                })
                .WithValidation<CampaignCommand>()
                .RequireAuthorization(AppPolicies.CampaignManager)
                .WithTags("Campaigns")
                .WithName("CreateCampaign")
                .Produces<CampaignResponse>(StatusCodes.Status201Created);
    }

    public sealed class Handler(AppDbContext db, TimeProvider clock) : IHandler
    {
        public async Task<CampaignResponse> HandleAsync(CampaignCommand command, CancellationToken ct)
        {
            var campaign = new Campaign { Name = command.Name, Title = command.Title };
            campaign.Apply(command);
            db.Campaigns.Add(campaign);
            await db.SaveChangesAsync(ct);

            var now = clock.GetUtcNow();
            var showingId = await db.Campaigns.Live(now).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct);
            return campaign.ToResponse(now, showingId);
        }
    }
}

/// <summary>PUT /api/admin/campaigns/{id}</summary>
public static class UpdateCampaign
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPut("/admin/campaigns/{id:guid}", async (Guid id, CampaignCommand command, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(id, command, ct)).Match(TypedResults.Ok))
                .WithValidation<CampaignCommand>()
                .RequireAuthorization(AppPolicies.CampaignManager)
                .WithTags("Campaigns")
                .WithName("UpdateCampaign")
                .Produces<CampaignResponse>()
                .ProducesProblem(StatusCodes.Status404NotFound);
    }

    public sealed class Handler(AppDbContext db, TimeProvider clock) : IHandler
    {
        public async Task<Result<CampaignResponse>> HandleAsync(Guid id, CampaignCommand command, CancellationToken ct)
        {
            var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (campaign is null)
            {
                return Error.NotFound("Campaign.NotFound", $"No existe la campaña {id}.");
            }

            campaign.Apply(command);
            await db.SaveChangesAsync(ct);

            var now = clock.GetUtcNow();
            var showingId = await db.Campaigns.Live(now).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct);
            return campaign.ToResponse(now, showingId);
        }
    }
}

/// <summary>DELETE /api/admin/campaigns/{id}</summary>
public static class DeleteCampaign
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapDelete("/admin/campaigns/{id:guid}", async (Guid id, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(id, ct)).Match(_ => TypedResults.NoContent()))
                .RequireAuthorization(AppPolicies.CampaignManager)
                .WithTags("Campaigns")
                .WithName("DeleteCampaign")
                .ProducesProblem(StatusCodes.Status404NotFound);
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<bool>> HandleAsync(Guid id, CancellationToken ct)
        {
            var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (campaign is null)
            {
                return Error.NotFound("Campaign.NotFound", $"No existe la campaña {id}.");
            }

            db.Campaigns.Remove(campaign);
            await db.SaveChangesAsync(ct);
            return true;
        }
    }
}
