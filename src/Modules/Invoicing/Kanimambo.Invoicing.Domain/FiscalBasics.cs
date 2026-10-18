using System.Text.RegularExpressions;

namespace Kanimambo.Invoicing.Domain;

/// <summary>
/// How a seller is taxed. IVA normal sellers charge and declare IVA; sellers in the simplified regime (ISPC,
/// Imposto Simplificado para Pequenos Contribuintes) pay a turnover tax instead and do not charge IVA.
/// </summary>
public enum FiscalRegime
{
    IvaNormal,
    Ispc,
}

public enum DocumentType
{
    Invoice,
    CreditNote,
}

/// <summary>Número Único de Identificação Tributária: nine digits. Only the format is checked here.</summary>
public static partial class Nuit
{
    public static bool IsValid(string? value) => value is not null && NuitPattern().IsMatch(value);

    [GeneratedRegex(@"^\d{9}$")]
    private static partial Regex NuitPattern();
}

/// <summary>Issuer or buyer of a fiscal document. Final consumers may have no NUIT.</summary>
public sealed record FiscalParty(string? Nuit, string Name, string? Address)
{
    public static FiscalParty FinalConsumer(string? nuit = null) => new(nuit is { Length: > 0 } ? nuit : null, "Consumidor final", null);
}

/// <summary>
/// One priced line of a fiscal document, all amounts in MZN. Prices include IVA: <see cref="Gross"/> is what the
/// customer pays for the line after <see cref="Discount"/>, and <see cref="Net"/> + <see cref="Iva"/> = <see cref="Gross"/>.
/// </summary>
public sealed record FiscalLine(
    int LineNumber,
    string Description,
    string? Sku,
    decimal Quantity,
    string TaxCategory,
    decimal Rate,
    decimal Discount,
    decimal Net,
    decimal Iva,
    decimal Gross,
    string? ExemptionReason);

/// <summary>A line before tax: what was sold, its category and the amount paid for it (IVA included), before discount.</summary>
public sealed record LineToPrice(string Description, string? Sku, decimal Quantity, string TaxCategory, decimal Gross, decimal Discount = 0m)
{
    public decimal Taxable => Gross - Discount;
}
