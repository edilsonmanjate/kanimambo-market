using Kanimambo.Invoicing.Contracts;
using Kanimambo.Invoicing.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Invoicing.Application.Regimes;

/// <summary>IVA normal: prices include IVA, so each line's IVA is extracted at its category's rate on the issue date.</summary>
internal sealed class IvaTaxCalculator(ITaxRateTable rates) : ITaxCalculator
{
    public FiscalRegime Regime => FiscalRegime.IvaNormal;

    public Result<FiscalLine> Price(int lineNumber, LineToPrice line, DateOnly on)
    {
        ArgumentNullException.ThrowIfNull(line);
        var rate = rates.RateFor(line.TaxCategory, on);
        if (rate.IsFailure)
        {
            return rate.Error;
        }

        var gross = line.Taxable;
        var net = Math.Round(gross / (1m + rate.Value.Rate), 2, MidpointRounding.AwayFromZero);
        return new FiscalLine(lineNumber, line.Description, line.Sku, line.Quantity, rate.Value.Category, rate.Value.Rate, line.Discount,
            net, gross - net, gross, rate.Value.Rate == 0m ? rate.Value.ExemptionReason : null);
    }
}
