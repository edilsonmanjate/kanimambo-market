using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Invoicing.Domain;

/// <summary>Every product of a regime family says which regime it belongs to.</summary>
public interface IRegimeComponent
{
    FiscalRegime Regime { get; }
}

/// <summary>Prices a line: IVA extracted at the category's rate (IVA normal), or not charged at all (ISPC).</summary>
public interface ITaxCalculator : IRegimeComponent
{
    Result<FiscalLine> Price(int lineNumber, LineToPrice line, DateOnly on);
}

/// <summary>Titles, numbering format and the legal mention printed on the document.</summary>
public interface IDocumentFormatter : IRegimeComponent
{
    string Title(DocumentType type);

    string FormatNumber(string series, int year, int number);

    /// <summary>Printed on every document of the regime (for ISPC: why no IVA is charged).</summary>
    string? LegalNotice { get; }
}

/// <summary>The numbering series of the regime, per document type (a separate series per regime keeps sequences clean).</summary>
public interface IDocumentSeries : IRegimeComponent
{
    string For(DocumentType type);
}

/// <summary>
/// Abstract Factory: creates the coherent family of components for one fiscal regime. The calculator, the
/// formatter and the numbering series of IVA normal must never be combined with those of ISPC.
/// </summary>
public interface IFiscalRegimeFactory
{
    FiscalRegime Regime { get; }

    ITaxCalculator CreateTaxCalculator();

    IDocumentFormatter CreateFormatter();

    IDocumentSeries CreateSeries();
}

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
