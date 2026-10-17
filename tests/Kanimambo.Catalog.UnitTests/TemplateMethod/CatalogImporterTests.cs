using System.Text;
using ClosedXML.Excel;
using Kanimambo.Catalog.Application.Imports;
using Kanimambo.Catalog.Infrastructure.Imports;

namespace Kanimambo.Catalog.UnitTests.TemplateMethod;

public sealed class CatalogImporterTests
{
    private const string CsvHeader = "sku;name;category;packaging;pack_size;unit;size;color;price;currency;stock;weight_kg;tax_category";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly RecordingBatchWriter _writer = new();

    [Fact]
    public async Task Csv_report_lists_accepted_rows_and_every_rejection_with_its_reasons()
    {
        var csv = string.Join('\n',
            CsvHeader,
            "ARROZ-25;Arroz agulha;Mercearia;saco;25;kg;;;1.850,00;MZN;40;25;exempt",
            "OLEO-5;Óleo de cozinha;Mercearia;bidão;5;l;;;720,50;MZN;60;4,6;standard",
            "BAD-1;Sem preço;Mercearia;saco;25;kg;;;abc;MZN;3;25;standard",
            "BAD-3;Stock negativo;Mercearia;saco;25;kg;;;100;MZN;-3;25;standard",
            "BAD-2;Taxa estranha;Mercearia;saco;5;kg;;;100;MZN;1;5;luxury",
            "ARROZ-25;Duplicado;Mercearia;saco;25;kg;;;1850;MZN;1;25;exempt",
            "SHORT;Faltam colunas;Mercearia");

        var report = await Import(new CsvCatalogImporter(_writer, FixedTaxRates.Mozambique), csv);

        report.TotalRows.ShouldBe(7);
        report.Created.ShouldBe(2);
        report.Rejected.Select(r => r.Line).ShouldBe([4, 5, 6, 7, 8]);
        report.Rejected[0].Reasons.ShouldContain(r => r.Contains("price", StringComparison.Ordinal));
        report.Rejected[1].Reasons.ShouldBe(["stock: Stock cannot be negative."]);
        report.Rejected[2].Reasons.ShouldBe(["Unknown tax category 'luxury'."]);
        report.Rejected[3].Reasons.ShouldBe(["SKU 'ARROZ-25' appears more than once in this import."]);
        report.Rejected[4].Reasons.ShouldContain(r => r.StartsWith("Expected 13 columns", StringComparison.Ordinal));

        var oil = _writer.All.Single(d => d.Details.Sku == "OLEO-5").Details;
        oil.Price.Amount.ShouldBe(720.50m);
        oil.WeightKg.ShouldBe(4.6m);
        _writer.All.Single(d => d.Details.Sku == "ARROZ-25").Details.Price.Amount.ShouldBe(1_850m);
    }

    [Fact]
    public async Task A_file_without_the_required_columns_is_rejected_as_a_whole()
    {
        var report = await Import(new CsvCatalogImporter(_writer, FixedTaxRates.Mozambique), "sku;name\nX;Y");

        report.Succeeded.ShouldBeFalse();
        report.FatalError!.ShouldContain("category");
        _writer.Batches.ShouldBeEmpty();
    }

    [Fact]
    public async Task Rows_are_persisted_in_fixed_size_batches()
    {
        var rows = Enumerable.Range(1, 450).Select(i => $"SKU-{i};Produto {i};mercearia;pacote;1;kg;;;10;MZN;5;1;standard");

        var report = await Import(new CsvCatalogImporter(_writer, FixedTaxRates.Mozambique), string.Join('\n', [CsvHeader, .. rows]));

        report.Created.ShouldBe(450);
        _writer.Batches.Select(b => b.Count).ShouldBe([CatalogImporter<object>.BatchSize, CatalogImporter<object>.BatchSize, 50]);
    }

    [Fact]
    public async Task Excel_rejects_text_typed_into_numeric_cells()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Produtos");
        string[] header = ["sku", "name", "category", "packaging", "pack_size", "unit", "price", "currency", "stock", "weight_kg", "tax_category"];
        for (var c = 0; c < header.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = header[c];
        }

        object[][] rows =
        [
            ["CAJU-1", "Castanha de caju", "Mercearia", "pacote", 1, "kg", 950.5, "MZN", 90, 1, "standard"],
            ["CAJU-500", "Castanha de caju", "Mercearia", "pacote", 500, "g", "quinhentos", "MZN", 150, 0.5, "standard"],
        ];
        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                sheet.Cell(r + 2, c + 1).Value = XLCellValue.FromObject(rows[r][c]);
            }
        }

        await using var content = new MemoryStream();
        workbook.SaveAs(content);
        content.Position = 0;

        var report = await new ExcelCatalogImporter(_writer, FixedTaxRates.Mozambique)
            .ImportAsync(TestCatalog.Mercearia, new ImportSource("caju.xlsx", content), Ct);

        report.Created.ShouldBe(1);
        report.Rejected.ShouldHaveSingleItem().Line.ShouldBe(3);
        report.Rejected[0].Reasons.ShouldBe(["Column 'price' must be a numeric cell (found text 'quinhentos')."]);
        _writer.All.Single().Details.Price.Amount.ShouldBe(950.50m);
    }

    [Fact]
    public async Task Not_a_workbook_is_a_fatal_error()
    {
        var report = await Import(new ExcelCatalogImporter(_writer, FixedTaxRates.Mozambique), "this is not a zip file");

        report.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Wholesaler_xml_maps_unit_codes_grams_and_root_currency()
    {
        const string xml = """
            <catalogo moeda="ZAR">
              <artigo codigo="LEB-001">
                <descricao>Leite UHT</descricao><categoria>Laticínios</categoria><unidade>CX12</unidade>
                <preco>215.00</preco><stock>40</stock><peso_gramas>12400</peso_gramas><iva>standard</iva>
              </artigo>
              <artigo codigo="LEB-002">
                <descricao>Massa</descricao><categoria>Mercearia</categoria><unidade>ZZ9</unidade>
                <preco>22.90</preco><stock>300</stock><iva>standard</iva>
              </artigo>
            </catalogo>
            """;

        var report = await Import(new XmlCatalogImporter(_writer, FixedTaxRates.Mozambique), xml);

        report.Created.ShouldBe(1);
        var milk = _writer.All.Single().Details;
        milk.Price.ToString().ShouldBe("215.00 ZAR");
        milk.WeightKg.ShouldBe(12.4m);
        milk.Variant.Describe().ShouldBe("caixa 12 un");
        report.Rejected.ShouldHaveSingleItem().Reasons.ShouldBe(["Element <peso_gramas> is missing."]);
    }

    [Fact]
    public async Task Malformed_xml_stops_the_import()
    {
        var report = await Import(new XmlCatalogImporter(_writer, FixedTaxRates.Mozambique), "<catalogo><artigo codigo=\"A\"><descricao>x</catalogo>");

        report.Succeeded.ShouldBeFalse();
        report.FatalError!.ShouldStartWith("Malformed XML");
    }

    private static async Task<ImportReport> Import(ICatalogImporter importer, string content)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        return await importer.ImportAsync(TestCatalog.Mercearia, new ImportSource("upload", stream), Ct);
    }
}
