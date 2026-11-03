using PrensEcommerce.Api.Common.Caching;
using PrensEcommerce.Api.Features.Crm;

namespace PrensEcommerce.Api.Features.Cart;

/// <summary>Línea del carrito guardada en Redis (solo Id y cantidad; el precio se recalcula al leer).</summary>
public sealed record CartLine(Guid ProductId, int Quantity);

/// <summary>
/// Carrito del usuario en Redis. La clave queda aislada por tenant gracias a <see cref="ITenantCache"/>.
/// Compartido solo por los slices de Cart y Checkout. Cada cambio se anota en el CRM
/// (<see cref="CartActivityTracker"/>) para la automatización de carrito abandonado.
/// </summary>
public sealed class CartStore(ITenantCache cache, CartActivityTracker activity)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    public async Task<List<CartLine>> GetAsync(Guid userId, CancellationToken ct) =>
        await cache.GetAsync<List<CartLine>>(Key(userId), ct) ?? [];

    public async Task SaveAsync(Guid userId, List<CartLine> lines, CancellationToken ct)
    {
        if (lines.Count == 0)
        {
            await cache.RemoveAsync(Key(userId), ct);
        }
        else
        {
            await cache.SetAsync(Key(userId), lines, Lifetime, ct);
        }

        await activity.TrackAsync(userId, lines.Sum(l => l.Quantity), ct);
    }

    public async Task ClearAsync(Guid userId, CancellationToken ct)
    {
        await cache.RemoveAsync(Key(userId), ct);
        await activity.TrackAsync(userId, 0, ct);
    }

    private static string Key(Guid userId) => $"cart:{userId:N}";
}
