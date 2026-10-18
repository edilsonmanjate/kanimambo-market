using Kanimambo.Invoicing.Application.Regimes;
using Kanimambo.Invoicing.Domain;
using static Kanimambo.Invoicing.UnitTests.TestFiscal;

namespace Kanimambo.Invoicing.UnitTests.AbstractFactory;

public sealed class FiscalRegimeFactoryTests
{
    [Fact]
    public void The_same_sale_under_ISPC_charges_no_IVA_and_says_why()
    {
        var invoice = Invoice(IspcKit());

        invoice.Regime.ShouldBe(FiscalRegime.Ispc);
        invoice.FullNumber.ShouldBe("FS 2026/000001");
        invoice.Iva.ShouldBe(0m);
        invoice.Net.ShouldBe(invoice.Gross);
        invoice.Lines.ShouldAllBe(l => l.Rate == 0m && l.ExemptionReason == "Não sujeito a IVA — ISPC");
        invoice.LegalNotice.ShouldBe("Regime do ISPC.");
    }

    [Fact]
    public void Each_factory_produces_a_family_of_its_own_regime()
    {
        IFiscalRegimeFactory[] factories = [new IvaNormalFactory(Rates(), IvaOptions), new IspcFactory(IspcOptions)];

        foreach (var factory in factories)
        {
            IRegimeComponent[] family = [factory.CreateTaxCalculator(), factory.CreateFormatter(), factory.CreateSeries()];
            family.ShouldAllBe(component => component.Regime == factory.Regime);
        }
    }

    [Fact]
    public void A_kit_cannot_mix_components_of_different_regimes()
    {
        var iva = new IvaNormalFactory(Rates(), IvaOptions);
        var ispc = new IspcFactory(IspcOptions);

        Should.Throw<ArgumentException>(() => new FiscalRegimeKit(iva.CreateTaxCalculator(), ispc.CreateFormatter(), iva.CreateSeries()))
            .Message.ShouldContain("cannot mix regimes");
        Should.Throw<ArgumentException>(() => new FiscalRegimeKit(ispc.CreateTaxCalculator(), ispc.CreateFormatter(), iva.CreateSeries()));
    }

    [Fact]
    public void Credit_notes_use_the_series_of_the_invoices_regime_and_refuse_another_kit()
    {
        var invoice = Invoice(IspcKit());

        var credit = CreditNote.ForRefund(invoice, IspcKit(), () => 1, Today, "Encomenda reembolsada", Now).Value;
        var wrong = CreditNote.ForRefund(invoice, IvaKit(), () => 1, Today, "Encomenda reembolsada", Now);

        credit.FullNumber.ShouldBe("NS 2026/000001");
        credit.Title.ShouldBe("Nota de Crédito");
        credit.CreditedInvoiceNumber.ShouldBe(invoice.FullNumber);
        credit.Gross.ShouldBe(invoice.Gross);
        wrong.Error.Code.ShouldBe("invoicing.regime_mismatch");
    }

    [Fact]
    public void A_credit_note_copies_the_invoices_lines_and_parties_without_taking_them()
    {
        var invoice = Invoice();

        var credit = CreditNote.ForRefund(invoice, IvaKit(), () => 1, Today, "Reembolso", Now).Value;

        // Owned values are stored per document: sharing instances would let the ORM move them to the credit note.
        credit.Lines.ShouldBe(invoice.Lines);
        credit.Lines.Zip(invoice.Lines).ShouldAllBe(pair => !ReferenceEquals(pair.First, pair.Second));
        ReferenceEquals(credit.Issuer, invoice.Issuer).ShouldBeFalse();
        ReferenceEquals(credit.Buyer, invoice.Buyer).ShouldBeFalse();
    }

    [Fact]
    public void The_registry_hands_out_whole_kits_by_regime()
    {
        var registry = new FiscalRegimeFactories([new IvaNormalFactory(Rates(), IvaOptions), new IspcFactory(IspcOptions)]);

        registry.KitFor(FiscalRegime.IvaNormal).Series.For(DocumentType.Invoice).ShouldBe("FT");
        registry.KitFor(FiscalRegime.Ispc).Series.For(DocumentType.CreditNote).ShouldBe("NS");
    }
}
