using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using TenantCore.Application.Common.Abstractions;

namespace TenantCore.Infrastructure.Caching;

public sealed class DistributedCacheService(IDistributedCache distributedCache) : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        var value = await distributedCache.GetStringAsync(key, cancellationToken);
        return value is null ? default : JsonSerializer.Deserialize<T>(value, JsonOptions);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(value, JsonOptions);

        return distributedCache.SetStringAsync(
            key,
            payload,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            },
            cancellationToken);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        return distributedCache.RemoveAsync(key, cancellationToken);
    }
}
