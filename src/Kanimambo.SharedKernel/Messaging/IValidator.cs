using Kanimambo.SharedKernel.Results;

namespace Kanimambo.SharedKernel.Messaging;

/// <summary>
/// Validates the shape of a request (required fields, formats, ranges) before its handler runs.
/// Rules that need state (stock, uniqueness) belong in the domain, not here.
/// </summary>
public interface IValidator<in TRequest>
{
    IEnumerable<FieldError> Validate(TRequest request);
}
