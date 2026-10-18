namespace Kanimambo.SharedKernel.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

    public int BatchSize { get; set; } = 50;

    public int MaxAttempts { get; set; } = 10;

    /// <summary>Retry delay after the first failure; doubles with each attempt (capped at five minutes).</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(2);

    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);
}
