using System.Globalization;
using System.Text.Json;
using Kanimambo.Payments.Application.Gateways;
using Kanimambo.SharedKernel.Results;
using Microsoft.Extensions.Caching.Distributed;

namespace Kanimambo.Payments.Infrastructure.Gateways.Decorators;

/// <summary>
/// Caches <em>final</em> statuses (confirmed / failed) in Redis: a settled transaction never changes, and
/// operators rate-limit status queries. Pending statuses are never cached.
/// </summary>
public sealed class CachingGatewayDecorator(IMobileMoneyGateway inner, IDistributedCache cache) : MobileMoneyGatewayDecorator(inner)
{
    private static readonly DistributedCacheEntryOptions Lifetime = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1) };

    public override async Task<Result<GatewayPaymentStatus>> QueryStatusAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var key = string.Create(CultureInfo.InvariantCulture, $"payments:status:{Operator}:{paymentId}");
        if (await cache.GetStringAsync(key, cancellationToken) is { } cached)
        {
            return JsonSerializer.Deserialize<GatewayPaymentStatus>(cached)!;
        }

        var result = await Inner.QueryStatusAsync(paymentId, cancellationToken);
        if (result.IsSuccess && result.Value.IsFinal)
        {
            await cache.SetStringAsync(key, JsonSerializer.Serialize(result.Value), Lifetime, cancellationToken);
        }

        return result;
    }
}
