namespace Kanimambo.Api.Idempotency;

internal enum IdempotencyRecordStatus
{
    InProgress,
    Completed,
}

/// <summary>Stored outcome of a write request, keyed by (scope, Idempotency-Key).</summary>
internal sealed class IdempotencyRecord
{
    public required string Scope { get; init; }

    public required string Key { get; init; }

    /// <summary>SHA-256 of query string + body: the same key with a different payload is rejected.</summary>
    public required string RequestFingerprint { get; init; }

    public IdempotencyRecordStatus Status { get; init; }

    public int? ResponseStatusCode { get; init; }

    public string? ResponseContentType { get; init; }

    public string? ResponseLocation { get; init; }

    public byte[]? ResponseBody { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }
}
