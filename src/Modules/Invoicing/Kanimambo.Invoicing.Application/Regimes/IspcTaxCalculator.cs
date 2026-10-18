using Kanimambo.Invoicing.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Invoicing.Application.Regimes;

/// <summary>ISPC: the seller does not charge IVA at all; every line says so (the category is kept for reference only).</summary>
internal sealed class IspcTaxCalculator(RegimeOptions options) : ITaxCalculator
{
    public FiscalRegime Regime => FiscalRegime.Ispc;

    public Result<FiscalLine> Price(int lineNumber, LineToPrice line, DateOnly on)
    {
        ArgumentNullException.ThrowIfNull(line);
        return new FiscalLine(lineNumber, line.Description, line.Sku, line.Quantity, line.TaxCategory, 0m, line.Discount,
            line.Taxable, 0m, line.Taxable, options.NotSubjectReason);
    }
}
