using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Crm;

/// <summary>Cliente con sus métricas agregadas. Se calcula en SQL; sirve para listados y segmentos.</summary>
public sealed class CustomerRow
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string? Phone { get; init; }

    public bool IsActive { get; init; }

    public bool AcceptsMarketing { get; init; }

    public List<string> Tags { get; init; } = [];

    public DateTimeOffset RegisteredAt { get; init; }

    public int OrderCount { get; init; }

    public decimal TotalSpent { get; init; }

    public DateTimeOffset? LastOrderAt { get; init; }

    public int CartItemCount { get; init; }

    public DateTimeOffset? CartUpdatedAt { get; init; }
}

public static class CustomerQueries
{
    /// <summary>
    /// Clientes de la tienda actual (el filtro global aplica el tenant) con sus pedidos no cancelados
    /// agregados. Excluye al personal: solo usuarios sin roles o con el rol Customer.
    /// </summary>
    public static IQueryable<CustomerRow> Customers(AppDbContext db) =>
        from u in db.Users.AsNoTracking()
        where u.Roles.All(r => r.Role!.Name == SystemRoles.Customer)
        join c in db.CartActivities.AsNoTracking() on u.Id equals c.UserId into carts
        from c in carts.DefaultIfEmpty()
        let orders = db.Orders.Where(o => o.UserId == u.Id && o.Status != OrderStatus.Cancelled)
        select new CustomerRow
        {
            Id = u.Id,
            Name = u.Name,
            Email = u.Email,
            Phone = u.Phone,
            IsActive = u.IsActive,
            AcceptsMarketing = u.AcceptsMarketing,
            Tags = u.Tags,
            RegisteredAt = u.CreatedAt,
            OrderCount = orders.Count(),
            TotalSpent = orders.Sum(o => (decimal?)o.Total) ?? 0m,
            LastOrderAt = orders.Max(o => (DateTimeOffset?)o.CreatedAt),
            CartItemCount = c == null ? 0 : c.ItemCount,
            CartUpdatedAt = c == null ? null : c.LastActivityAt,
        };

    /// <summary>Aplica las reglas de un segmento (todas con AND).</summary>
    public static IQueryable<CustomerRow> Matching(this IQueryable<CustomerRow> query, SegmentRules rules, DateTimeOffset now)
    {
        if (rules.MinOrders is { } minOrders)
        {
            query = query.Where(c => c.OrderCount >= minOrders);
        }

        if (rules.MaxOrders is { } maxOrders)
        {
            query = query.Where(c => c.OrderCount <= maxOrders);
        }

        if (rules.MinTotalSpent is { } minSpent)
        {
            query = query.Where(c => c.TotalSpent >= minSpent);
        }

        if (rules.LastOrderOlderThanDays is { } olderThan)
        {
            var cutoff = now.AddDays(-olderThan);
            query = query.Where(c => c.LastOrderAt != null && c.LastOrderAt <= cutoff);
        }

        if (rules.LastOrderWithinDays is { } within)
        {
            var cutoff = now.AddDays(-within);
            query = query.Where(c => c.LastOrderAt >= cutoff);
        }

        if (rules.RegisteredWithinDays is { } registered)
        {
            var cutoff = now.AddDays(-registered);
            query = query.Where(c => c.RegisteredAt >= cutoff);
        }

        if (rules.HasCartItems is { } hasCart)
        {
            query = hasCart ? query.Where(c => c.CartItemCount > 0) : query.Where(c => c.CartItemCount == 0);
        }

        if (rules.AcceptsMarketing is { } accepts)
        {
            query = query.Where(c => c.AcceptsMarketing == accepts);
        }

        if (!string.IsNullOrWhiteSpace(rules.Tag))
        {
            var tag = CustomerTags.Normalize(rules.Tag);
            query = query.Where(c => c.Tags.Contains(tag));
        }

        return query;
    }
}
