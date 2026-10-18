namespace Kanimambo.Invoicing.Domain;

/// <summary>Issuer or buyer of a fiscal document. Final consumers may have no NUIT.</summary>
public sealed record FiscalParty(string? Nuit, string Name, string? Address)
{
    public static FiscalParty FinalConsumer(string? nuit = null) => new(nuit is { Length: > 0 } ? nuit : null, "Consumidor final", null);
}
