using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Application.Products;

public sealed class CreateBundleValidator : IValidator<CreateBundle>
{
    public IEnumerable<FieldError> Validate(CreateBundle request)
    {
        if (request.Components is not { Count: > 0 })
        {
            yield return new FieldError(nameof(request.Components), "A bundle needs at least one component.");
            yield break;
        }

        if (request.Components.Any(c => c.Quantity < 1))
        {
            yield return new FieldError(nameof(request.Components), "Every component quantity must be at least 1.");
        }
    }
}
