namespace Kanimambo.SharedKernel.Results;

/// <summary>
/// Category of a business error. The HTTP layer maps each category to a status code,
/// so the domain never needs to know about HTTP.
/// </summary>
public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict,
    BusinessRule,
    Unauthorized,
    Forbidden,

    /// <summary>A dependency (gateway, external API) is temporarily unavailable; retrying later may succeed.</summary>
    Unavailable,
}
