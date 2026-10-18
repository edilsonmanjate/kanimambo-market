using Kanimambo.Invoicing.Domain;
using Kanimambo.SharedKernel.Results;
using static Kanimambo.Invoicing.UnitTests.TestFiscal;

namespace Kanimambo.Invoicing.UnitTests.Builder;

public sealed class InvoiceBuilderTests
{
    [Fact]
    public void A_valid_invoice_extracts_IVA_from_prices_that_include_it()
    {
        var invoice = Valid().Build(() => 7, Now).Value;

        invoice.FullNumber.ShouldBe("FT 2026/000007");
        invoice.Title.ShouldBe("Fatura");
        invoice.Lines.Select(l => (l.TaxCategory, l.Rate, l.Net, l.Iva, l.Gross)).ShouldBe([
            ("standard", 0.16m, 862.07m, 137.93m, 1_000m),       // 1 000 / 1.16
            ("exempt", 0m, 1_850m, 0m, 1_850m),
        ]);
        invoice.Lines[1].ExemptionReason.ShouldBe("Isento — artigo 9.º do CIVA");
        (invoice.Net, invoice.Iva, invoice.Gross).ShouldBe((2_712.07m, 137.93m, 2_850m));
        invoice.Buyer.Name.ShouldBe("Consumidor final");
        invoice.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<FiscalDocumentIssued>().Number.ShouldBe("FT 2026/000007");
    }

    [Fact]
    public void A_discount_reduces_the_taxable_amount_and_its_IVA()
    {
        var invoice = Valid()
            .AddLine(new LineToPrice("Açúcar 10 kg", "ACUCAR", 1m, "standard", 580m, Discount: 58m))
            .Build(() => 1, Now).Value;

        var sugar = invoice.Lines[^1];
        (sugar.Discount, sugar.Gross, sugar.Net, sugar.Iva).ShouldBe((58m, 522m, 450m, 72m));
    }

    [Fact]
    public void Every_problem_is_reported_at_once()
    {
        var result = InvoiceBuilder.For(IvaKit())
            .IssuedBy(Seller with { Nuit = "12345" })
            .BilledTo(new FiscalParty("ABC", "Restaurante Polana", null))
            .Build(() => 1, Now);

        var fields = result.Error.ShouldBeOfType<ValidationError>().Errors.Select(e => e.Field).ToList();
        fields.ShouldBe(["issuer.nuit", "buyer.nuit", "issueDate", "order", "lines"], ignoreOrder: true);
    }

    [Fact]
    public void Business_buyers_need_their_NUIT()
    {
        var result = Valid().BilledTo(new FiscalParty(null, "Restaurante Polana", null), nuitRequired: true).Build(() => 1, Now);

        result.Error.ShouldBeOfType<ValidationError>().Errors.ShouldHaveSingleItem().Field.ShouldBe("buyer.nuit");
    }

    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(1, -1, 0)]
    [InlineData(1, 100, 101)]
    public void Lines_need_a_positive_quantity_and_sane_amounts(decimal quantity, decimal gross, decimal discount) =>
        Valid().AddLine(new LineToPrice("X", null, quantity, "standard", gross, discount)).Build(() => 1, Now).IsFailure.ShouldBeTrue();

    [Fact]
    public void Unknown_tax_categories_are_refused_not_charged_at_zero() =>
        Valid().AddLine(new LineToPrice("Perfume", null, 1m, "luxury", 900m)).Build(() => 1, Now)
            .Error.ShouldBeOfType<ValidationError>().Errors.ShouldHaveSingleItem().Message.ShouldContain("luxury");

    [Fact]
    public void An_untaxed_line_without_a_legal_reason_is_refused()
    {
        var kit = new FiscalRegimeKit(new ZeroRateWithoutReason(), IvaKit().Formatter, IvaKit().Series);

        var result = Valid(kit).Build(() => 1, Now);

        result.Error.ShouldBeOfType<ValidationError>().Errors.ShouldAllBe(e => e.Field.EndsWith("exemptionReason", StringComparison.Ordinal));
    }

    [Fact]
    public void A_number_is_taken_only_when_the_invoice_is_valid()
    {
        var counter = new DocumentCounter(Seller.Nuit!, "FT", 2026);

        Valid().AddLine(new LineToPrice("X", null, 0m, "standard", 1m)).Build(counter.Next, Now).IsFailure.ShouldBeTrue();
        counter.LastNumber.ShouldBe(0);

        Valid().Build(counter.Next, Now).Value.Number.ShouldBe(1);
        Valid().Build(counter.Next, Now).Value.Number.ShouldBe(2);
    }

    [Fact]
    public void The_rate_in_force_on_the_issue_date_applies() =>
        Valid().IssuedOn(new DateOnly(2022, 12, 31)).Build(() => 1, Now).Value.Lines[0].Rate.ShouldBe(0.17m);

    private sealed class ZeroRateWithoutReason : ITaxCalculator
    {
        public FiscalRegime Regime => FiscalRegime.IvaNormal;

        public Result<FiscalLine> Price(int lineNumber, LineToPrice line, DateOnly on) =>
            new FiscalLine(lineNumber, line.Description, line.Sku, line.Quantity, line.TaxCategory, 0m, 0m, line.Gross, 0m, line.Gross, null);
    }
}
