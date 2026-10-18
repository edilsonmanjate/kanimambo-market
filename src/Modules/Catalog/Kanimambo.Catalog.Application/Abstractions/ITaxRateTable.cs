using Kanimambo.Catalog.Domain.Products;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Application.Abstractions;

/// <summary>IVA (and future) rates per tax category, from configuration — never hard-coded.</summary>
public interface ITaxRateTable
{
    Result<decimal> RateFor(TaxCategory category);
}
