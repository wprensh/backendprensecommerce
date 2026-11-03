using System.Security.Claims;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Reviews;

/// <summary>
/// GET /api/products/{id}/reviews — opiniones visibles de un producto, con la media, el reparto por
/// estrellas y, si hay sesión, la opinión propia y si el cliente puede opinar.
/// </summary>
public static class GetProductReviews
{
    public sealed record Response(
        decimal Average,
        int Count,
        IReadOnlyList<int> Distribution,
        IReadOnlyList<ReviewResponse> Items,
        ReviewResponse? Mine,
        bool CanReview,
        bool HasPurchased);

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/products/{id:guid}/reviews", async (Guid id, ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(id, user.Identity?.IsAuthenticated == true ? user.GetUserId() : null, ct))
                    .Match(TypedResults.Ok))
                .AllowAnonymous()
                .WithTags("Reviews")
                .WithName("GetProductReviews");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        private const int MaxItems = 50;

        public async Task<Result<Response>> HandleAsync(Guid productId, Guid? userId, CancellationToken ct)
        {
            if (!await db.Products.AnyAsync(p => p.Id == productId && p.IsActive, ct))
            {
                return Error.NotFound("Reviews.ProductNotFound", "El producto no existe.");
            }

            var visible = db.ProductReviews.AsNoTracking().Where(r => r.ProductId == productId && !r.IsHidden);

            var byStars = await visible.GroupBy(r => r.Rating)
                .Select(g => new { Stars = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Stars, g => g.Count, ct);
            var count = byStars.Values.Sum();
            var average = count == 0
                ? 0
                : Math.Round(byStars.Sum(kv => kv.Key * kv.Value) / (decimal)count, 1, MidpointRounding.AwayFromZero);
            // Índice 0 = 1 estrella … índice 4 = 5 estrellas.
            var distribution = Enumerable.Range(1, 5).Select(s => byStars.GetValueOrDefault(s)).ToList();

            var items = await visible.OrderByDescending(r => r.CreatedAt).Take(MaxItems).ToListAsync(ct);

            ReviewResponse? mine = null;
            var purchased = false;
            if (userId is { } uid)
            {
                var own = await db.ProductReviews.AsNoTracking().FirstOrDefaultAsync(r => r.ProductId == productId && r.UserId == uid, ct);
                mine = own is null ? null : ReviewResponse.From(own);
                purchased = await ReviewRules.HasPurchasedAsync(db, uid, productId, ct);
            }

            return new Response(average, count, distribution, items.Select(ReviewResponse.From).ToList(),
                mine, userId is not null, purchased);
        }
    }
}

/// <summary>PUT /api/products/{id}/reviews/mine — crea o edita la opinión del cliente sobre el producto.</summary>
public static class UpsertMyReview
{
    public sealed record Command(int Rating, string? Title, string Comment);

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Rating).InclusiveBetween(1, 5).WithMessage("La puntuación va de 1 a 5 estrellas.");
            RuleFor(c => c.Title).MaximumLength(150);
            RuleFor(c => c.Comment).NotEmpty().WithMessage("Cuéntanos qué te pareció el producto.")
                .MaximumLength(2000);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPut("/products/{id:guid}/reviews/mine",
                    async (Guid id, Command command, ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                        (await handler.HandleAsync(id, user.GetUserId(), command, ct)).Match(TypedResults.Ok))
                .RequireAuthorization()
                .WithValidation<Command>()
                .WithTags("Reviews")
                .WithName("UpsertMyReview");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<ReviewResponse>> HandleAsync(Guid productId, Guid userId, Command command, CancellationToken ct)
        {
            if (!await db.Products.AnyAsync(p => p.Id == productId && p.IsActive, ct))
            {
                return Error.NotFound("Reviews.ProductNotFound", "El producto no existe.");
            }

            var author = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Name).FirstOrDefaultAsync(ct);
            if (author is null)
            {
                return Error.NotFound("Account.NotFound", "La cuenta no existe.");
            }

            var review = await db.ProductReviews.FirstOrDefaultAsync(r => r.ProductId == productId && r.UserId == userId, ct);
            if (review is null)
            {
                review = new ProductReview { ProductId = productId, UserId = userId, AuthorName = author };
                db.ProductReviews.Add(review);
            }

            review.AuthorName = author;
            review.Rating = command.Rating;
            review.Title = string.IsNullOrWhiteSpace(command.Title) ? null : command.Title.Trim();
            review.Comment = command.Comment.Trim();
            review.IsVerifiedPurchase = await ReviewRules.HasPurchasedAsync(db, userId, productId, ct);

            await db.SaveChangesAsync(ct);
            await ReviewRules.RefreshProductRatingAsync(db, productId, ct);

            return ReviewResponse.From(review);
        }
    }
}

/// <summary>DELETE /api/products/{id}/reviews/mine — retira la opinión propia.</summary>
public static class DeleteMyReview
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapDelete("/products/{id:guid}/reviews/mine", async (Guid id, ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                {
                    await handler.HandleAsync(id, user.GetUserId(), ct);
                    return TypedResults.NoContent();
                })
                .RequireAuthorization()
                .WithTags("Reviews")
                .WithName("DeleteMyReview");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task HandleAsync(Guid productId, Guid userId, CancellationToken ct)
        {
            var deleted = await db.ProductReviews.Where(r => r.ProductId == productId && r.UserId == userId).ExecuteDeleteAsync(ct);
            if (deleted > 0)
            {
                await ReviewRules.RefreshProductRatingAsync(db, productId, ct);
            }
        }
    }
}
