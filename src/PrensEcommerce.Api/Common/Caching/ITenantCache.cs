using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using PrensEcommerce.Api.Infrastructure.MultiTenancy;

namespace PrensEcommerce.Api.Common.Caching;

/// <summary>
/// Caché en Redis aislada por tenant: todas las claves llevan el prefijo <c>t:{tenantId}:</c>,
/// así un slice nunca puede leer datos de otra tienda aunque use la misma clave lógica.
/// </summary>
public interface ITenantCache
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken);

    Task SetAsync<T>(string key, T value, TimeSpan slidingExpiration, CancellationToken cancellationToken);

    Task RemoveAsync(string key, CancellationToken cancellationToken);
}

internal sealed class RedisTenantCache(IDistributedCache cache, ITenantProvider tenantProvider) : ITenantCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        var bytes = await cache.GetAsync(Scoped(key), cancellationToken);
        return bytes is null ? default : JsonSerializer.Deserialize<T>(bytes, JsonOptions);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan slidingExpiration, CancellationToken cancellationToken) =>
        cache.SetAsync(
            Scoped(key),
            JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions),
            new DistributedCacheEntryOptions { SlidingExpiration = slidingExpiration },
            cancellationToken);

    public Task RemoveAsync(string key, CancellationToken cancellationToken) =>
        cache.RemoveAsync(Scoped(key), cancellationToken);

    private string Scoped(string key) => $"t:{tenantProvider.TenantId:N}:{key}";
}
