using PrensEcommerce.Api.Common.Caching;

namespace PrensEcommerce.Api.Features.Cart;

/// <summary>Línea del carrito guardada en Redis (solo Id y cantidad; el precio se recalcula al leer).</summary>
public sealed record CartLine(Guid ProductId, int Quantity);

/// <summary>
/// Carrito del usuario en Redis. La clave queda aislada por tenant gracias a <see cref="ITenantCache"/>.
/// Compartido solo por los slices de Cart y Checkout.
/// </summary>
public sealed class CartStore(ITenantCache cache)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    public async Task<List<CartLine>> GetAsync(Guid userId, CancellationToken ct) =>
        await cache.GetAsync<List<CartLine>>(Key(userId), ct) ?? [];

    public Task SaveAsync(Guid userId, List<CartLine> lines, CancellationToken ct) =>
        lines.Count == 0
            ? cache.RemoveAsync(Key(userId), ct)
            : cache.SetAsync(Key(userId), lines, Lifetime, ct);

    public Task ClearAsync(Guid userId, CancellationToken ct) => cache.RemoveAsync(Key(userId), ct);

    private static string Key(Guid userId) => $"cart:{userId:N}";
}
