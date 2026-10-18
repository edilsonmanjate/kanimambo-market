using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Kanimambo.SharedKernel.Identity;
using static Kanimambo.Invoicing.IntegrationTests.Journey;

namespace Kanimambo.Invoicing.IntegrationTests;

/// <summary>OrderPaid (outbox) → one invoice per seller, in the seller's own regime, plus the marketplace's delivery invoice.</summary>
public sealed class InvoicingTests(InvoicingApiFactory factory)
{
    private readonly HttpClient _customer = factory.CreateClientAs($"cliente-{Guid.NewGuid():N}");
    private readonly HttpClient _backoffice = factory.Backoffice;

    [Fact]
    public async Task A_paid_order_is_invoiced_per_seller_in_each_regime_and_the_invoices_add_up_to_the_total()
    {
        var (ivaSeller, ivaProducts) = await factory.CreateSellerAsync(("OLEO", 1_000m, "standard"), ("ARROZ", 1_850m, "exempt"));
        var (ispcSeller, ispcProducts) = await factory.CreateSellerAsync(("CAJU", 400m, "standard"));
        await _backoffice.RegisterTaxpayerAsync(ivaSeller, "IvaNormal");
        await _backoffice.RegisterTaxpayerAsync(ispcSeller, "Ispc");

        var order = await _customer.PaidOrderAsync(_backoffice, "MPesa", null, (ivaProducts["OLEO"], 2), (ivaProducts["ARROZ"], 1), (ispcProducts["CAJU"], 1));
        var documents = await _backoffice.DocumentsAsync(order.GetProperty("orderId").GetGuid(), 3);

        documents.Sum(d => d.GetProperty("gross").GetDecimal()).ShouldBe(order.GetProperty("total").GetDecimal());

        var iva = documents.Single(d => d.GetProperty("sellerId").ValueKind != JsonValueKind.Null && d.GetProperty("regime").GetString() == "IvaNormal");
        iva.GetProperty("number").GetString().ShouldStartWith("FT ");
        iva.GetProperty("lines").EnumerateArray().Select(l => (l.GetProperty("taxCategory").GetString(), l.GetProperty("rate").GetDecimal()))
            .ShouldBe([("standard", 0.16m), ("exempt", 0m)], ignoreOrder: true);
        iva.GetProperty("iva").GetDecimal().ShouldBeGreaterThan(0m);

        var ispc = documents.Single(d => d.GetProperty("regime").GetString() == "Ispc");
        ispc.GetProperty("number").GetString().ShouldStartWith("FS ");
        ispc.GetProperty("iva").GetDecimal().ShouldBe(0m);
        ispc.GetProperty("legalNotice").GetString().ShouldNotBeNull().ShouldContain("ISPC");

        var delivery = documents.Single(d => d.GetProperty("sellerId").ValueKind == JsonValueKind.Null);
        delivery.GetProperty("issuerName").GetString().ShouldBe("Kanimambo Market, Lda");
        delivery.GetProperty("gross").GetDecimal().ShouldBe(order.GetProperty("shippingFee").GetDecimal());
    }

    [Fact]
    public async Task A_promotion_discount_is_spread_over_the_lines_and_the_invoices_still_add_up()
    {
        // Seeded promotion: "M-Pesa em Maputo: 5% acima de 2 500 MZN".
        var (seller, products) = await factory.CreateSellerAsync(("OLEO", 1_299.99m, "standard"), ("FEIJAO", 733.33m, "reduced"));
        await _backoffice.RegisterTaxpayerAsync(seller, "IvaNormal");

        var order = await _customer.PaidOrderAsync(_backoffice, "MPesa", null, (products["OLEO"], 1), (products["FEIJAO"], 2));
        order.GetProperty("discount").GetDecimal().ShouldBeGreaterThan(0m);
        var documents = await _backoffice.DocumentsAsync(order.GetProperty("orderId").GetGuid(), 2);

        documents.Sum(d => d.GetProperty("gross").GetDecimal()).ShouldBe(order.GetProperty("total").GetDecimal());
        var goods = documents.Single(d => d.GetProperty("sellerId").ValueKind != JsonValueKind.Null);
        goods.GetProperty("lines").EnumerateArray().Sum(l => l.GetProperty("discount").GetDecimal()).ShouldBe(order.GetProperty("discount").GetDecimal());
    }

    [Fact]
    public async Task A_bundle_mixing_exempt_and_taxed_goods_becomes_one_line_per_tax_category()
    {
        var (seller, products) = await factory.CreateSellerAsync(("ARROZ", 400m, "exempt"), ("OLEO", 200m, "standard"));
        await _backoffice.RegisterTaxpayerAsync(seller, "IvaNormal");
        var bundle = await _backoffice.PostAsJsonAsync($"/api/catalog/sellers/{seller}/bundles", new
        {
            sku = $"CABAZ-{Guid.NewGuid():N}"[..20],
            name = "Cabaz de teste",
            category = "cabazes",
            components = new[] { new { productId = products["ARROZ"], quantity = 2 }, new { productId = products["OLEO"], quantity = 1 } },
        }, Ct).Json(HttpStatusCode.Created);

        var order = await _customer.PaidOrderAsync(_backoffice, "Card", null, (bundle.GetGuid(), 1));
        var goods = (await _backoffice.DocumentsAsync(order.GetProperty("orderId").GetGuid(), 2)).Single(d => d.GetProperty("sellerId").ValueKind != JsonValueKind.Null);

        // 2 × 400 exempt + 1 × 200 taxed; the seeded "cabazes" promotion is spread over both parts.
        goods.GetProperty("lines").EnumerateArray()
            .Select(l => (l.GetProperty("taxCategory").GetString(), l.GetProperty("gross").GetDecimal() + l.GetProperty("discount").GetDecimal()))
            .ShouldBe([("exempt", 800m), ("standard", 200m)], ignoreOrder: true);
        goods.GetProperty("gross").GetDecimal().ShouldBe(order.GetProperty("subtotal").GetDecimal() - order.GetProperty("discount").GetDecimal());
    }

    [Fact]
    public async Task Business_customers_are_invoiced_with_their_NUIT()
    {
        var (seller, products) = await factory.CreateSellerAsync(("ARROZ-50", 3_500m, "exempt"));
        await _backoffice.RegisterTaxpayerAsync(seller, "IvaNormal");
        var restaurant = factory.CreateClientAs($"restaurante-{Guid.NewGuid():N}", KnownRoles.B2B);

        var order = await restaurant.PaidOrderAsync(_backoffice, "MPesa", "400123456", (products["ARROZ-50"], 2));
        var goods = (await _backoffice.DocumentsAsync(order.GetProperty("orderId").GetGuid(), 2)).Single(d => d.GetProperty("sellerId").ValueKind != JsonValueKind.Null);

        goods.GetProperty("buyerNuit").GetString().ShouldBe("400123456");
        goods.GetProperty("buyerName").GetString().ShouldStartWith("restaurante-");
    }

    [Fact]
    public async Task A_seller_without_fiscal_data_gets_its_invoice_as_soon_as_it_registers()
    {
        var (seller, products) = await factory.CreateSellerAsync(("MEL", 650m, "standard"));
        var order = await _customer.PaidOrderAsync(_backoffice, "MPesa", null, (products["MEL"], 1));
        var orderId = order.GetProperty("orderId").GetGuid();
        await _backoffice.DocumentsAsync(orderId, 1); // only the delivery invoice

        await EventuallyAsync(async () => (await _backoffice.GetFromJsonAsync<JsonElement[]>(new Uri("/api/invoicing/requests?status=AwaitingTaxpayer", UriKind.Relative), Ct))!
            .Any(r => r.GetProperty("orderId").GetGuid() == orderId), "the seller's invoice waits for its fiscal data");

        var registered = await _backoffice.RegisterTaxpayerAsync(seller, "Ispc");

        registered.GetProperty("invoicesIssuedNow").GetInt32().ShouldBe(1);
        (await _backoffice.DocumentsAsync(orderId, 2)).ShouldContain(d => d.GetProperty("regime").GetString() == "Ispc");
    }

    [Fact]
    public async Task A_refund_credits_every_invoice_and_the_IVA_summary_nets_to_zero()
    {
        var (seller, products) = await factory.CreateSellerAsync(("PAINEL", 4_640m, "standard"));
        var nuit = NewNuit();
        await _backoffice.RegisterTaxpayerAsync(seller, "IvaNormal", nuit);
        var order = await _customer.PaidOrderAsync(_backoffice, "Card", null, (products["PAINEL"], 1));
        var orderId = order.GetProperty("orderId").GetGuid();
        await _backoffice.DocumentsAsync(orderId, 2);

        await _backoffice.PostAsJsonAsync($"/api/orders/{orderId}/transitions/Refund", new { note = "Produto danificado" }, Ct).Json();

        var documents = await _backoffice.DocumentsAsync(orderId, 4);
        var credits = documents.Where(d => d.GetProperty("type").GetString() == "CreditNote").ToList();
        credits.Count.ShouldBe(2);
        credits.ShouldAllBe(c => c.GetProperty("creditedInvoiceNumber").GetString()!.StartsWith("FT ", StringComparison.Ordinal));

        var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(2));
        var summary = await _backoffice.GetFromJsonAsync<JsonElement>(new Uri($"/api/invoicing/iva-summary/{nuit}/{today.Year}/{today.Month}", UriKind.Relative), Ct);
        (summary.GetProperty("invoices").GetInt32(), summary.GetProperty("creditNotes").GetInt32()).ShouldBe((1, 1));
        summary.GetProperty("iva").GetDecimal().ShouldBe(0m);
    }

    [Fact]
    public async Task The_monthly_e_Declaracao_file_lists_the_issuers_documents_in_csv_and_xlsx_and_xml_waits_for_its_flag()
    {
        var (seller, products) = await factory.CreateSellerAsync(("OLEO", 1_000m, "standard"));
        var nuit = NewNuit();
        await _backoffice.RegisterTaxpayerAsync(seller, "IvaNormal", nuit);
        var order = await _customer.PaidOrderAsync(_backoffice, "Card", null, (products["OLEO"], 1));
        var invoice = (await _backoffice.DocumentsAsync(order.GetProperty("orderId").GetGuid(), 2)).Single(d => d.GetProperty("issuerNuit").GetString() == nuit);
        var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(2));
        var path = $"/api/invoicing/e-declaracao/{nuit}/{today.Year}/{today.Month}";

        var csv = await _backoffice.GetAsync(new Uri($"{path}?format=csv", UriKind.Relative), Ct);
        var xlsx = await _backoffice.GetAsync(new Uri($"{path}?format=xlsx", UriKind.Relative), Ct);
        var xml = await _backoffice.GetAsync(new Uri($"{path}?format=xml", UriKind.Relative), Ct);

        csv.Content.Headers.ContentType!.MediaType.ShouldBe("text/csv");
        (await csv.Content.ReadAsStringAsync(Ct)).ShouldContain($"{nuit};FT;{invoice.GetProperty("number").GetString()};");
        xlsx.Content.Headers.ContentType!.MediaType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        xml.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await xml.Content.ReadAsStringAsync(Ct)).ShouldContain("feature.disabled");
    }

    [Fact]
    public async Task Each_document_can_be_downloaded_as_a_pdf()
    {
        var (seller, products) = await factory.CreateSellerAsync(("OLEO", 1_000m, "standard"));
        await _backoffice.RegisterTaxpayerAsync(seller, "IvaNormal");
        var order = await _customer.PaidOrderAsync(_backoffice, "Card", null, (products["OLEO"], 1));
        var invoice = (await _backoffice.DocumentsAsync(order.GetProperty("orderId").GetGuid(), 2))[0];

        var pdf = await _backoffice.GetAsync(new Uri($"/api/invoicing/documents/{invoice.GetProperty("id").GetGuid()}/pdf", UriKind.Relative), Ct);

        pdf.StatusCode.ShouldBe(HttpStatusCode.OK);
        pdf.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        Encoding.Latin1.GetString(await pdf.Content.ReadAsByteArrayAsync(Ct)).ShouldStartWith("%PDF-1.4");
    }

    [Fact]
    public async Task Invoice_numbers_are_sequential_per_issuer_and_series()
    {
        var (seller, products) = await factory.CreateSellerAsync(("OLEO", 1_000m, "standard"));
        var nuit = NewNuit();
        await _backoffice.RegisterTaxpayerAsync(seller, "IvaNormal", nuit);

        var numbers = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var order = await _customer.PaidOrderAsync(_backoffice, "Card", null, (products["OLEO"], 1));
            numbers.Add((await _backoffice.DocumentsAsync(order.GetProperty("orderId").GetGuid(), 2))
                .Single(d => d.GetProperty("issuerNuit").GetString() == nuit).GetProperty("number").GetString()!);
        }

        var year = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(2)).Year;
        numbers.ShouldBe([$"FT {year}/000001", $"FT {year}/000002", $"FT {year}/000003"]);
    }

    [Fact]
    public async Task The_catalog_and_the_invoices_read_the_same_rate_table()
    {
        var rates = await factory.CreateClient().GetFromJsonAsync<JsonElement[]>(new Uri("/api/invoicing/tax-rates", UriKind.Relative), Ct);
        var historic = await factory.CreateClient().GetFromJsonAsync<JsonElement[]>(new Uri("/api/invoicing/tax-rates?on=2022-06-01", UriKind.Relative), Ct);

        rates!.Single(r => r.GetProperty("category").GetString() == "standard").GetProperty("rate").GetDecimal().ShouldBe(0.16m);
        historic!.Single(r => r.GetProperty("category").GetString() == "standard").GetProperty("rate").GetDecimal().ShouldBe(0.17m);
    }

    [Fact]
    public async Task Fiscal_data_is_validated_and_a_NUIT_belongs_to_one_seller()
    {
        var (sellerA, _) = await factory.CreateSellerAsync(("A", 10m, "standard"));
        var (sellerB, _) = await factory.CreateSellerAsync(("B", 10m, "standard"));
        var nuit = NewNuit();
        await _backoffice.RegisterTaxpayerAsync(sellerA, "IvaNormal", nuit);

        var badNuit = await _backoffice.PutAsJsonAsync($"/api/invoicing/taxpayers/{sellerB}", new { nuit = "12AB", legalName = "Loja", regime = "IvaNormal" }, Ct);
        var badRegime = await _backoffice.PutAsJsonAsync($"/api/invoicing/taxpayers/{sellerB}", new { nuit = NewNuit(), legalName = "Loja", regime = "Flat" }, Ct);
        var taken = await _backoffice.PutAsJsonAsync($"/api/invoicing/taxpayers/{sellerB}", new { nuit, legalName = "Loja", regime = "IvaNormal" }, Ct);

        badNuit.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        badRegime.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        taken.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Fiscal_documents_are_back_office_only() =>
        (await _customer.GetAsync(new Uri($"/api/invoicing/documents?orderId={Guid.NewGuid()}", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
}
