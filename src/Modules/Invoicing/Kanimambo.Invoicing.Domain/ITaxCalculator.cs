using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Invoicing.Domain;

/// <summary>Prices a line: IVA extracted at the category's rate (IVA normal), or not charged at all (ISPC).</summary>
public interface ITaxCalculator : IRegimeComponent
{
    Result<FiscalLine> Price(int lineNumber, LineToPrice line, DateOnly on);
}
