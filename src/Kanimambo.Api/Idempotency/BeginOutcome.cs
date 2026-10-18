namespace Kanimambo.Api.Idempotency;

internal enum BeginOutcome
{
    Started,
    Replay,
    InFlight,
    FingerprintMismatch,
}
