using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kanimambo.Api.Idempotency;

internal enum BeginOutcome
{
    Started,
    Replay,
    InFlight,
    FingerprintMismatch,
}

internal sealed record BeginResult(BeginOutcome Outcome, IdempotencyRecord? Existing = null);

/// <summary>
/// Postgres-backed store. The primary key (scope, key) is the lock: whoever inserts the
/// in-progress row first executes the request, everybody else replays or is told to wait.
/// </summary>
internal sealed class IdempotencyStore(
    IdempotencyDbContext db,
    TimeProvider timeProvider,
    IOptions<IdempotencyOptions> options)
{
    public async Task<BeginResult> TryBeginAsync(string scope, string key, string fingerprint, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var abandonedBefore = now - options.Value.InFlightTimeout;

        await db.Records
            .Where(r => r.Scope == scope && r.Key == key
                && (r.ExpiresAt <= now || (r.Status == IdempotencyRecordStatus.InProgress && r.CreatedAt <= abandonedBefore)))
            .ExecuteDeleteAsync(cancellationToken);

        // ON CONFLICT DO NOTHING: losing the race is an expected outcome, not an exception (and not an error log).
        var expiresAt = now + options.Value.RecordTimeToLive;
        var inProgress = nameof(IdempotencyRecordStatus.InProgress);
        var inserted = await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO platform."IdempotencyRecords" ("Scope", "Key", "RequestFingerprint", "Status", "CreatedAt", "ExpiresAt")
            VALUES ({scope}, {key}, {fingerprint}, {inProgress}, {now}, {expiresAt})
            ON CONFLICT ("Scope", "Key") DO NOTHING
            """,
            cancellationToken);

        if (inserted == 1)
        {
            return new BeginResult(BeginOutcome.Started);
        }

        var existing = await db.Records.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Scope == scope && r.Key == key, cancellationToken);

        return existing switch
        {
            null => new BeginResult(BeginOutcome.InFlight),
            _ when existing.RequestFingerprint != fingerprint => new BeginResult(BeginOutcome.FingerprintMismatch),
            { Status: IdempotencyRecordStatus.InProgress } => new BeginResult(BeginOutcome.InFlight),
            _ => new BeginResult(BeginOutcome.Replay, existing),
        };
    }

    public Task CompleteAsync(
        string scope,
        string key,
        int statusCode,
        string? contentType,
        string? location,
        byte[] body,
        CancellationToken cancellationToken) =>
        db.Records
            .Where(r => r.Scope == scope && r.Key == key)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(r => r.Status, IdempotencyRecordStatus.Completed)
                    .SetProperty(r => r.ResponseStatusCode, statusCode)
                    .SetProperty(r => r.ResponseContentType, contentType)
                    .SetProperty(r => r.ResponseLocation, location)
                    .SetProperty(r => r.ResponseBody, body),
                cancellationToken);

    /// <summary>Removes the in-progress row so the client can retry (used after 5xx or exceptions).</summary>
    public Task ReleaseAsync(string scope, string key, CancellationToken cancellationToken) =>
        db.Records.Where(r => r.Scope == scope && r.Key == key).ExecuteDeleteAsync(cancellationToken);

    public Task<int> DeleteExpiredAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        return db.Records.Where(r => r.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);
    }
}
