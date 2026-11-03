using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Reviews;

public sealed record ReviewResponse(
    Guid Id, Guid ProductId, string AuthorName, int Rating, string? Title, string Comment,
    bool IsVerifiedPurchase, bool IsHidden, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt)
{
    public static ReviewResponse From(ProductReview r) =>
        new(r.Id, r.ProductId, r.AuthorName, r.Rating, r.Title, r.Comment, r.IsVerifiedPurchase, r.IsHidden, r.CreatedAt, r.UpdatedAt);
}

public static class ReviewRules
{
    /// <summary>Estados en los que el pedido cuenta como compra (ya cobrado).</summary>
    public static readonly OrderStatus[] PurchasedStatuses = [OrderStatus.Paid, OrderStatus.Shipped, OrderStatus.Delivered];

    public static Task<bool> HasPurchasedAsync(AppDbContext db, Guid userId, Guid productId, CancellationToken ct) =>
        db.Orders.AnyAsync(o => o.UserId == userId
                                && PurchasedStatuses.Contains(o.Status)
                                && o.Items.Any(i => i.ProductId == productId), ct);

    /// <summary>
    /// Recalcula la media y el número de opiniones visibles del producto. Se llama tras guardar
    /// cualquier cambio en sus opiniones, así el catálogo no tiene que agregarlas en cada consulta.
    /// </summary>
    public static async Task RefreshProductRatingAsync(AppDbContext db, Guid productId, CancellationToken ct)
    {
        var stats = await db.ProductReviews.AsNoTracking()
            .Where(r => r.ProductId == productId && !r.IsHidden)
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Average = g.Average(r => (decimal)r.Rating) })
            .FirstOrDefaultAsync(ct);

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null)
        {
            return;
        }

        product.ReviewCount = stats?.Count ?? 0;
        product.Rating = stats is null ? 0 : Math.Round(stats.Average, 1, MidpointRounding.AwayFromZero);
        await db.SaveChangesAsync(ct);
    }
}
