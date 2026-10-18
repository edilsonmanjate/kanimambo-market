namespace Kanimambo.Invoicing.Domain;

/// <summary>
/// The family of one regime, as one value. Documents are built from a kit, never from loose components, and the
/// kit refuses components of different regimes — so mixing them is impossible, not just discouraged.
/// </summary>
public sealed class FiscalRegimeKit
{
    public FiscalRegimeKit(ITaxCalculator calculator, IDocumentFormatter formatter, IDocumentSeries series)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        ArgumentNullException.ThrowIfNull(formatter);
        ArgumentNullException.ThrowIfNull(series);
        if (formatter.Regime != calculator.Regime || series.Regime != calculator.Regime)
        {
            throw new ArgumentException(
                $"A fiscal kit cannot mix regimes (calculator {calculator.Regime}, formatter {formatter.Regime}, series {series.Regime}).");
        }

        Regime = calculator.Regime;
        Calculator = calculator;
        Formatter = formatter;
        Series = series;
    }

    public FiscalRegime Regime { get; }

    public ITaxCalculator Calculator { get; }

    public IDocumentFormatter Formatter { get; }

    public IDocumentSeries Series { get; }

    public static FiscalRegimeKit From(IFiscalRegimeFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return new FiscalRegimeKit(factory.CreateTaxCalculator(), factory.CreateFormatter(), factory.CreateSeries());
    }
}
