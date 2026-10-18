using Kanimambo.Invoicing.Domain;

namespace Kanimambo.Invoicing.Application.Regimes;

/// <summary>The registered factories by regime: the issuer's regime picks the whole family, never single components.</summary>
public sealed class FiscalRegimeFactories(IEnumerable<IFiscalRegimeFactory> factories)
{
    private readonly Dictionary<FiscalRegime, FiscalRegimeKit> _kits = factories.ToDictionary(f => f.Regime, FiscalRegimeKit.From);

    public FiscalRegimeKit KitFor(FiscalRegime regime) =>
        _kits.TryGetValue(regime, out var kit) ? kit : throw new InvalidOperationException($"No fiscal regime factory registered for {regime}.");
}
