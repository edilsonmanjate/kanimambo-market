using System.Text.Json.Serialization;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Application.Backoffice;

/// <summary>
/// Command pattern: a back-office request as an object. It can be executed now, stored (serialized, with
/// whatever state it captured while executing) and undone later from the audit log.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(AdjustSellerPricesOperation), AdjustSellerPricesOperation.KindName)]
[JsonDerivedType(typeof(SuspendSellerOperation), SuspendSellerOperation.KindName)]
public interface IBackofficeOperation
{
    [JsonIgnore]
    string Kind { get; }

    string Describe();

    Task<Result> ExecuteAsync(BackofficeContext context, CancellationToken cancellationToken);

    Task<Result> UndoAsync(BackofficeContext context, CancellationToken cancellationToken);
}
