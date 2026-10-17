using System.Globalization;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.Catalog.Domain.Variants;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Infrastructure.Imports;

/// <summary>Column layout shared by the seller spreadsheet formats (CSV and Excel).</summary>
internal static class TabularColumns
{
    public const string Sku = "sku";
    public const string Name = "name";
    public const string Category = "category";
    public const string Packaging = "packaging";
    public const string PackSize = "pack_size";
    public const string Unit = "unit";
    public const string Size = "size";
    public const string Color = "color";
    public const string Price = "price";
    public const string Currency = "currency";
    public const string Stock = "stock";
    public const string WeightKg = "weight_kg";
    public const string TaxCategory = "tax_category";

    public static readonly string[] Required = [Sku, Name, Category, Packaging, PackSize, Unit, Price, Currency, Stock, WeightKg, TaxCategory];

    /// <summary>Builds product details from text cells, collecting every problem instead of stopping at the first.</summary>
    public static Result<ProductDetails> ToDetails(Func<string, string?> cell, Func<string, (bool Ok, decimal Value)> number)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(number);

        var errors = new List<FieldError>();

        var packSize = number(PackSize);
        var price = number(Price);
        var weight = number(WeightKg);
        if (!packSize.Ok)
        {
            errors.Add(new FieldError(PackSize, $"'{cell(PackSize)}' is not a number."));
        }

        if (!price.Ok)
        {
            errors.Add(new FieldError(Price, $"'{cell(Price)}' is not a number."));
        }

        if (!weight.Ok)
        {
            errors.Add(new FieldError(WeightKg, $"'{cell(WeightKg)}' is not a number."));
        }

        if (!ImportValues.TryParseInt(cell(Stock), out var stock))
        {
            errors.Add(new FieldError(Stock, $"'{cell(Stock)}' is not a whole number."));
        }

        var currency = SharedKernel.Domain.Currency.FromCode(cell(Currency));
        var taxCategory = Domain.Products.TaxCategory.From(cell(TaxCategory));
        var variant = packSize.Ok
            ? VariantAttributesPool.Shared.Get(cell(Packaging), packSize.Value, cell(Unit), cell(Size), cell(Color))
            : null;

        Collect(errors, currency);
        Collect(errors, taxCategory);
        if (variant is not null)
        {
            Collect(errors, variant);
        }

        if (errors.Count > 0)
        {
            return new ValidationError(errors);
        }

        return new ProductDetails(
            cell(Sku) ?? string.Empty,
            cell(Name) ?? string.Empty,
            cell(Category) ?? string.Empty,
            variant!.Value,
            Money.Of(price.Value, currency.Value),
            stock,
            weight.Value,
            taxCategory.Value);
    }

    public static string Describe(IEnumerable<string> missing) =>
        string.Create(CultureInfo.InvariantCulture, $"Missing required columns: {string.Join(", ", missing)}.");

    private static void Collect(List<FieldError> errors, Result result)
    {
        if (result.IsFailure)
        {
            errors.AddRange(result.Error is ValidationError v ? v.Errors : [new FieldError("row", result.Error.Message)]);
        }
    }
}
