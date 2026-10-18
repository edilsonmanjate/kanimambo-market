using System.Text.RegularExpressions;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Ordering.Application.Acceptance;

/// <summary>
/// Business customers invoice with their NUIT (9 digits). Only the format is checked here; the official
/// check-digit rule is not published in a form we can rely on, so it is deliberately not invented.
/// </summary>
public sealed partial class BusinessCustomersNeedValidNuit : OrderAcceptanceHandler
{
    protected override Task<Result> CheckAsync(OrderAcceptanceContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var nuit = context.Customer.Nuit;

        if (context.Customer.IsBusiness && nuit.Length == 0)
        {
            return Task.FromResult(Result.Failure(Error.Validation(new FieldError("nuit", "Business orders need the company NUIT."))));
        }

        return Task.FromResult(nuit.Length == 0 || NuitPattern().IsMatch(nuit)
            ? Result.Success()
            : Result.Failure(Error.Validation(new FieldError("nuit", "A NUIT has exactly 9 digits."))));
    }

    [GeneratedRegex(@"^\d{9}$")]
    private static partial Regex NuitPattern();
}
