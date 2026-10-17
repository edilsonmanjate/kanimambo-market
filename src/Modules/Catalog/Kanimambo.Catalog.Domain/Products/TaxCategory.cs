using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Domain.Products;

/// <summary>
/// Tax classification of a product (e.g. <c>standard</c>, <c>reduced</c>, <c>exempt</c>). Only the code lives
/// in the catalog; the rate for each code comes from configuration because rates change by law.
/// </summary>
public sealed record TaxCategory
{
    private TaxCategory(string code) => Code = code;

    public string Code { get; }

    public static Result<TaxCategory> From(string? code) =>
        string.IsNullOrWhiteSpace(code) || code.Length > 32
            ? Error.Validation(new FieldError("taxCategory", "Tax category is required (max 32 characters)."))
            : new TaxCategory(code.Trim().ToLowerInvariant());

    public override string ToString() => Code;
}

/// <summary>Net amount subject to one tax category (a line of the IVA breakdown).</summary>
public sealed record TaxableAmount(TaxCategory Category, SharedKernel.Domain.Money Net);
