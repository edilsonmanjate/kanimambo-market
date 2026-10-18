namespace Kanimambo.Api.Idempotency;

internal sealed record BeginResult(BeginOutcome Outcome, IdempotencyRecord? Existing = null);
