namespace Kanimambo.Catalog.Domain.Products;

/// <summary>Amount (IVA included, as priced) subject to one tax category: a line of the IVA breakdown.</summary>
public sealed record TaxableAmount(TaxCategory Category, SharedKernel.Domain.Money Gross);
