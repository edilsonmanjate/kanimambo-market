using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Application.Backoffice;

public sealed class AdjustSellerPricesValidator : IValidator<AdjustSellerPrices>
{
    public const decimal MinPercentage = -50m;
    public const decimal MaxPercentage = 100m;

    public IEnumerable<FieldError> Validate(AdjustSellerPrices request)
    {
        if (request.Percentage == 0m || request.Percentage < MinPercentage || request.Percentage > MaxPercentage)
        {
            yield return new FieldError(nameof(request.Percentage), $"Percentage must be non-zero, between {MinPercentage} and {MaxPercentage}.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            yield return new FieldError(nameof(request.Reason), "A reason is required for the audit log.");
        }
    }
}
