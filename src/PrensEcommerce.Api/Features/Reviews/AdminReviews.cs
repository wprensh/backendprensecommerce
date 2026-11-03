using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Authorization;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Reviews;

public sealed record AdminReview(ReviewResponse Review, string ProductName, string CustomerEmail);

/// <summary>GET /api/admin/reviews?search=&amp;rating=&amp;visibility=&amp;page=&amp;pageSize= — moderación de opiniones.</summary>
public static class GetAdminReviews
{
    /// <param name="Visibility">"visible", "hidden" o vacío para todas.</param>
    public sealed record Query(string? Search, int? Rating, string? Visibility, int Page = 1, int PageSize = 20);

    public sealed record Response(IReadOnlyList<AdminReview> Items, int Page, int PageSize, int TotalCount, int HiddenCount);

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/admin/reviews", async ([AsParameters] Query query, Handler handler, CancellationToken ct) =>
                    TypedResults.Ok(await handler.HandleAsync(query, ct)))
                .RequirePermission(Permissions.CatalogRead)
                .WithTags("Admin · Reviews")
                .WithName("AdminGetReviews");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Response> HandleAsync(Query query, CancellationToken ct)
        {
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            var rows =
                from r in db.ProductReviews.AsNoTracking()
                join p in db.Products.AsNoTracking() on r.ProductId equals p.Id
                join u in db.Users.AsNoTracking() on r.UserId equals u.Id
                select new { Review = r, ProductName = p.Name, u.Email };

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var pattern = $"%{query.Search.Trim()}%";
                rows = rows.Where(x => EF.Functions.ILike(x.ProductName, pattern)
                                       || EF.Functions.ILike(x.Review.AuthorName, pattern)
                                       || EF.Functions.ILike(x.Email, pattern)
                                       || EF.Functions.ILike(x.Review.Comment, pattern));
            }

            if (query.Rating is >= 1 and <= 5)
            {
                rows = rows.Where(x => x.Review.Rating == query.Rating);
            }

            var hiddenCount = await rows.CountAsync(x => x.Review.IsHidden, ct);

            rows = query.Visibility?.ToLowerInvariant() switch
            {
                "hidden" => rows.Where(x => x.Review.IsHidden),
                "visible" => rows.Where(x => !x.Review.IsHidden),
                _ => rows,
            };

            var total = await rows.CountAsync(ct);
            var items = await rows.OrderByDescending(x => x.Review.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync(ct);

            return new Response(
                items.Select(x => new AdminReview(ReviewResponse.From(x.Review), x.ProductName, x.Email)).ToList(),
                page, pageSize, total, hiddenCount);
        }
    }
}

/// <summary>PATCH /api/admin/reviews/{id} — oculta o vuelve a mostrar una opinión y recalcula la media.</summary>
public static class SetReviewVisibility
{
    public sealed record Command(bool IsHidden);

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPatch("/admin/reviews/{id:guid}", async (Guid id, Command command, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(id, command, ct)).Match(TypedResults.Ok))
                .RequirePermission(Permissions.CatalogWrite)
                .WithTags("Admin · Reviews")
                .WithName("AdminSetReviewVisibility");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<ReviewResponse>> HandleAsync(Guid id, Command command, CancellationToken ct)
        {
            var review = await db.ProductReviews.FirstOrDefaultAsync(r => r.Id == id, ct);
            if (review is null)
            {
                return Error.NotFound("Reviews.NotFound", "La opinión no existe.");
            }

            if (review.IsHidden != command.IsHidden)
            {
                review.IsHidden = command.IsHidden;
                await db.SaveChangesAsync(ct);
                await ReviewRules.RefreshProductRatingAsync(db, review.ProductId, ct);
            }

            return ReviewResponse.From(review);
        }
    }
}
