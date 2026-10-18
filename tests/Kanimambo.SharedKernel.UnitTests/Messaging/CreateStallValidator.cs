using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.SharedKernel.UnitTests.Messaging;

public sealed class CreateStallValidator : IValidator<CreateStall>
{
    public IEnumerable<FieldError> Validate(CreateStall request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            yield return new FieldError(nameof(request.Name), "Name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.City))
        {
            yield return new FieldError(nameof(request.City), "City is required.");
        }
    }
}
