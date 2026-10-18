namespace Kanimambo.Invoicing.Domain;

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
