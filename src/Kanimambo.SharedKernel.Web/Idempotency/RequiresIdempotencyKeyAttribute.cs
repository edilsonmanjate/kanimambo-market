namespace Kanimambo.SharedKernel.Web.Idempotency;

/// <summary>
/// Every write endpoint accepts an <c>Idempotency-Key</c> header. Endpoints where a duplicate would be
/// harmful (checkout, payment initiation) also <em>require</em> it: a request without the header is rejected.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresIdempotencyKeyAttribute : Attribute;

public static class IdempotencyHeaders
{
    public const string Key = "Idempotency-Key";

    /// <summary>Set on responses replayed from the idempotency store instead of being re-executed.</summary>
    public const string Replayed = "Idempotent-Replayed";
}
