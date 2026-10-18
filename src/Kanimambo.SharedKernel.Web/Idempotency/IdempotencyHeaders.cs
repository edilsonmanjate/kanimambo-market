namespace Kanimambo.SharedKernel.Web.Idempotency;

public static class IdempotencyHeaders
{
    public const string Key = "Idempotency-Key";

    /// <summary>Set on responses replayed from the idempotency store instead of being re-executed.</summary>
    public const string Replayed = "Idempotent-Replayed";
}
