namespace Kanimambo.SharedKernel.Time;

/// <summary>
/// Mozambique uses Central Africa Time (UTC+02:00) all year, without daylight saving. A fixed offset
/// avoids depending on the host's time-zone database (unavailable with invariant globalization on Windows).
/// </summary>
public static class MozambiqueTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(2);

    public static DateTimeOffset Now(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return timeProvider.GetUtcNow().ToOffset(Offset);
    }

    public static DateOnly Today(TimeProvider timeProvider) => DateOnly.FromDateTime(Now(timeProvider).DateTime);

    /// <summary>The next local midnight, e.g. when a "rate of the day" stops being valid.</summary>
    public static DateTimeOffset EndOfDay(TimeProvider timeProvider)
    {
        var tomorrow = Today(timeProvider).AddDays(1);
        return new DateTimeOffset(tomorrow.ToDateTime(TimeOnly.MinValue), Offset);
    }
}
