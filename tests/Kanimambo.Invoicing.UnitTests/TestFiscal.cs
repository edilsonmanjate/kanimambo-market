using Kanimambo.Invoicing.Application.Regimes;
using Kanimambo.Invoicing.Application.Tax;
using Kanimambo.Invoicing.Contracts;
using Kanimambo.Invoicing.Domain;

namespace Kanimambo.Invoicing.UnitTests;

internal static class TestFiscal
{
    public static readonly DateTimeOffset Now = new(2026, 10, 19, 8, 0, 0, TimeSpan.Zero);
    public static readonly DateOnly Today = new(2026, 10, 19);
    public static readonly FiscalParty Seller = new("400100002", "Grossista da Matola, SA", "Machava, Matola");
    public static readonly Guid OrderId = Guid.NewGuid();

    public static TaxRate[] MozambicanRates =>
    [
        new("standard", 0.17m, new DateOnly(2008, 1, 1), null),
        new("standard", 0.16m, new DateOnly(2023, 1, 1), null),
        new("reduced", 0.05m, new DateOnly(2023, 1, 1), null),
        new("exempt", 0m, new DateOnly(2008, 1, 1), "Isento — artigo 9.º do CIVA"),
    ];

    public static TaxRateTable Rates() => new(MozambicanRates);

    public static RegimeOptions IvaOptions => new() { InvoiceSeries = "FT", CreditNoteSeries = "NC", LegalNotice = "Preços com IVA incluído." };

    public static RegimeOptions IspcOptions => new() { InvoiceSeries = "FS", CreditNoteSeries = "NS", LegalNotice = "Regime do ISPC.", NotSubjectReason = "Não sujeito a IVA — ISPC" };

    public static FiscalRegimeKit IvaKit(TaxRateTable? rates = null) => FiscalRegimeKit.From(new IvaNormalFactory(rates ?? Rates(), IvaOptions));

    public static FiscalRegimeKit IspcKit() => FiscalRegimeKit.From(new IspcFactory(IspcOptions));

    /// <summary>A complete, valid invoice in the making: callers break one part to test a rule.</summary>
    public static InvoiceBuilder Valid(FiscalRegimeKit? kit = null) =>
        InvoiceBuilder.For(kit ?? IvaKit())
            .IssuedBy(Seller)
            .IssuedOn(Today)
            .ForOrder(OrderId, 1002, Guid.NewGuid(), "258841234567")
            .AddLine(new LineToPrice("Óleo de girassol 5 L", "OLEO-5", 2m, "standard", 1_000m))
            .AddLine(new LineToPrice("Arroz 25 kg", "ARROZ-25", 1m, "exempt", 1_850m));

    public static Invoice Invoice(FiscalRegimeKit? kit = null, int number = 1) => Valid(kit).Build(() => number, Now).Value;
}
