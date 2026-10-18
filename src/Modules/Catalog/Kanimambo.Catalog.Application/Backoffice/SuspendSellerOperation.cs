using System.Text.Json.Serialization;
using Kanimambo.Catalog.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Application.Backoffice;

/// <summary>"Suspend seller" (e.g. a fraud investigation). Undo reactivates, unless already reactivated.</summary>
public sealed class SuspendSellerOperation : IBackofficeOperation
{
    public const string KindName = "suspend-seller";

    public required Guid SellerId { get; init; }

    public required string Reason { get; init; }

    [JsonIgnore]
    public string Kind => KindName;

    public string Describe() => $"Suspend seller {SellerId}: {Reason}";

    public async Task<Result> ExecuteAsync(BackofficeContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var seller = await context.Sellers.GetAsync(new SellerId(SellerId), cancellationToken);
        return seller is null
            ? CatalogErrors.SellerNotFound(SellerId)
            : seller.Suspend(Reason, context.TimeProvider.GetUtcNow());
    }

    public async Task<Result> UndoAsync(BackofficeContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var seller = await context.Sellers.GetAsync(new SellerId(SellerId), cancellationToken);
        return seller is null
            ? CatalogErrors.SellerNotFound(SellerId)
            : seller.Reactivate(context.TimeProvider.GetUtcNow());
    }
}
