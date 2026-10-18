namespace Kanimambo.Api.IntegrationTests.Probes;

public sealed record CounterOutput(string Bucket, int Count, string? Note);
