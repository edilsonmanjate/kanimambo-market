namespace Kanimambo.Api.Idempotency;

public sealed class IdempotencyOptions
{
    public const string SectionName = "Idempotency";

    /// <summary>How long a completed response can be replayed.</summary>
    public TimeSpan RecordTimeToLive { get; set; } = TimeSpan.FromHours(24);

    /// <summary>After this, an in-progress record is considered abandoned (e.g. the process crashed).</summary>
    public TimeSpan InFlightTimeout { get; set; } = TimeSpan.FromMinutes(2);

    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);
}
