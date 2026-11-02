using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Infrastructure.MultiTenancy;

/// <summary>
/// Busca el tenant en PostgreSQL y lo cachea con HybridCache (memoria L1 + Redis L2),
/// así la resolución no consulta la base de datos en cada petición.
/// </summary>
internal sealed class CachedTenantStore(
    AppDbContext db,
    HybridCache cache,
    IOptions<MultiTenancyOptions> options) : ITenantStore
{
    public const string CacheTag = "tenants";

    public async Task<TenantInfo?> FindAsync(string identifier, CancellationToken cancellationToken)
    {
        var normalized = identifier.Trim().ToLowerInvariant();
        var expiration = TimeSpan.FromMinutes(options.Value.CacheMinutes);

        return await cache.GetOrCreateAsync(
            $"tenant:{normalized}",
            (db, normalized),
            static async (state, ct) => await LoadAsync(state.db, state.normalized, ct),
            new HybridCacheEntryOptions { Expiration = expiration, LocalCacheExpiration = expiration },
            tags: [CacheTag],
            cancellationToken: cancellationToken);
    }

    private static async Task<TenantInfo?> LoadAsync(AppDbContext db, string identifier, CancellationToken ct)
    {
        var query = db.Tenants.AsNoTracking().Where(t => t.IsActive);

        query = Guid.TryParse(identifier, out var id)
            ? query.Where(t => t.Id == id)
            : query.Where(t => t.Slug == identifier);

        return await query
            .Select(t => new TenantInfo(t.Id, t.Slug, t.Name))
            .FirstOrDefaultAsync(ct);
    }
}
