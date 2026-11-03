using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Crm.Segments;

public sealed record SegmentResponse(Guid Id, string Name, string Description, SegmentRules Rules, int CustomerCount);

/// <summary>Cuerpo común de alta y edición de segmentos.</summary>
public sealed record SegmentCommand(string Name, string? Description, SegmentRules Rules);

public sealed class SegmentCommandValidator : AbstractValidator<SegmentCommand>
{
    public SegmentCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(120);
        RuleFor(c => c.Description).MaximumLength(500);
        RuleFor(c => c.Rules).NotNull();
        RuleFor(c => c.Rules.MinOrders).GreaterThanOrEqualTo(0);
        RuleFor(c => c.Rules.MaxOrders).GreaterThanOrEqualTo(0);
        RuleFor(c => c.Rules.MinTotalSpent).GreaterThanOrEqualTo(0);
        RuleFor(c => c.Rules.LastOrderOlderThanDays).InclusiveBetween(1, 3650);
        RuleFor(c => c.Rules.LastOrderWithinDays).InclusiveBetween(1, 3650);
        RuleFor(c => c.Rules.RegisteredWithinDays).InclusiveBetween(1, 3650);
        RuleFor(c => c.Rules.Tag).MaximumLength(40);
    }
}

/// <summary>GET /api/crm/segments — segmentos con el número de clientes que cumplen cada uno ahora.</summary>
public static class GetSegments
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/crm/segments", async (Handler handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(ct)))
                .RequireCrmRead()
                .WithTags("CRM")
                .WithName("GetCrmSegments");
    }

    public sealed class Handler(AppDbContext db, TimeProvider clock) : IHandler
    {
        public async Task<IReadOnlyList<SegmentResponse>> HandleAsync(CancellationToken ct)
        {
            var now = clock.GetUtcNow();
            var segments = await db.CustomerSegments.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct);
            var result = new List<SegmentResponse>(segments.Count);

            foreach (var s in segments)
            {
                var count = await CustomerQueries.Customers(db).Matching(s.Rules, now).CountAsync(ct);
                result.Add(new SegmentResponse(s.Id, s.Name, s.Description, s.Rules, count));
            }

            return result;
        }
    }
}

/// <summary>POST /api/crm/segments/preview — cuántos clientes cumplirían unas reglas, sin guardarlas.</summary>
public static class PreviewSegment
{
    public sealed record Response(int CustomerCount);

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("/crm/segments/preview", async (SegmentRules rules, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
                    TypedResults.Ok(new Response(await CustomerQueries.Customers(db).Matching(rules, clock.GetUtcNow()).CountAsync(ct))))
                .RequireCrmRead()
                .WithTags("CRM")
                .WithName("PreviewCrmSegment");
    }
}

/// <summary>POST /api/crm/segments y PUT /api/crm/segments/{id}.</summary>
public static class SaveSegment
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("/crm/segments", async (SegmentCommand command, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(null, command, ct)).Match(r => TypedResults.Created($"/api/crm/segments/{r.Id}", r)))
                .WithValidation<SegmentCommand>()
                .RequireCrmWrite()
                .WithTags("CRM")
                .WithName("CreateCrmSegment");

            app.MapPut("/crm/segments/{id:guid}", async (Guid id, SegmentCommand command, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(id, command, ct)).Match(TypedResults.Ok))
                .WithValidation<SegmentCommand>()
                .RequireCrmWrite()
                .WithTags("CRM")
                .WithName("UpdateCrmSegment");
        }
    }

    public sealed class Handler(AppDbContext db, TimeProvider clock) : IHandler
    {
        public async Task<Result<SegmentResponse>> HandleAsync(Guid? id, SegmentCommand command, CancellationToken ct)
        {
            var name = command.Name.Trim();
            if (await db.CustomerSegments.AnyAsync(s => s.Name == name && s.Id != id, ct))
            {
                return Error.Conflict("Segment.DuplicateName", "Ya existe un segmento con ese nombre.");
            }

            CustomerSegment? segment;
            if (id is null)
            {
                segment = new CustomerSegment { Name = name };
                db.CustomerSegments.Add(segment);
            }
            else
            {
                segment = await db.CustomerSegments.FirstOrDefaultAsync(s => s.Id == id, ct);
                if (segment is null)
                {
                    return Error.NotFound("Segment.NotFound", "El segmento no existe.");
                }

                segment.Name = name;
            }

            segment.Description = command.Description?.Trim() ?? string.Empty;
            segment.Rules = command.Rules;
            if (!string.IsNullOrWhiteSpace(segment.Rules.Tag))
            {
                segment.Rules.Tag = CustomerTags.Normalize(segment.Rules.Tag);
            }

            await db.SaveChangesAsync(ct);

            var count = await CustomerQueries.Customers(db).Matching(segment.Rules, clock.GetUtcNow()).CountAsync(ct);
            return new SegmentResponse(segment.Id, segment.Name, segment.Description, segment.Rules, count);
        }
    }
}

/// <summary>DELETE /api/crm/segments/{id}.</summary>
public static class DeleteSegment
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapDelete("/crm/segments/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
                    await db.CustomerSegments.Where(s => s.Id == id).ExecuteDeleteAsync(ct) == 0
                        ? Error.NotFound("Segment.NotFound", "El segmento no existe.").ToProblem()
                        : TypedResults.NoContent())
                .RequireCrmWrite()
                .WithTags("CRM")
                .WithName("DeleteCrmSegment");
    }
}
