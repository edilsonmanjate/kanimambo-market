using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Application.Backoffice;

public sealed class SuspendSellerValidator : IValidator<SuspendSeller>
{
    public IEnumerable<FieldError> Validate(SuspendSeller request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            yield return new FieldError(nameof(request.Reason), "A reason is required for the audit log.");
        }
    }
}
