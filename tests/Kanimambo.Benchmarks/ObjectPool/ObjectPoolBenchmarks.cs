using BenchmarkDotNet.Attributes;
using Kanimambo.Invoicing.Application;
using Kanimambo.Invoicing.Application.Regimes;
using Kanimambo.Invoicing.Application.Tax;
using Kanimambo.Invoicing.Contracts;
using Kanimambo.Invoicing.Domain;
using Kanimambo.Invoicing.Infrastructure.Documents;
using Microsoft.Extensions.ObjectPool;

namespace Kanimambo.Benchmarks.ObjectPool;

/// <summary>
/// Month-end bulk generation: every invoice of a busy seller as PDF, and the monthly e-Declaração CSV — with pooled
/// writers and string builders, and with the same code allocating fresh ones for every document.
/// </summary>
[MemoryDiagnoser]
public class ObjectPoolBenchmarks
{
    private FiscalDocument[] _documents = [];
    private FiscalPeriod _period = null!;
    private FiscalDocumentPdfRenderer _pooledPdf = null!;
    private FiscalDocumentPdfRenderer _freshPdf = null!;
    private MonthlyDeclarationExporter _pooledCsv = null!;
    private MonthlyDeclarationExporter _freshCsv = null!;

    [Params(500)]
    public int Documents { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _documents = MonthOfInvoices(Documents);
        _period = new FiscalPeriod("400100002", "Grossista da Matola, SA", 2026, 10, _documents);

        var pooled = InvoicingPools.Create(new DefaultObjectPoolProvider());
        var fresh = InvoicingPools.WithoutPooling();
        _pooledPdf = new FiscalDocumentPdfRenderer(pooled);
        _freshPdf = new FiscalDocumentPdfRenderer(fresh);
        _pooledCsv = new MonthlyDeclarationExporter(pooled);
        _freshCsv = new MonthlyDeclarationExporter(fresh);
    }

    [Benchmark(Baseline = true, Description = "PDFs, new writer per document")]
    public long PdfsWithoutPool() => RenderAll(_freshPdf);

    [Benchmark(Description = "PDFs, pooled writers")]
    public long PdfsWithPool() => RenderAll(_pooledPdf);

    [Benchmark(Description = "Monthly CSV x 20 sellers, new buffers")]
    public long CsvWithoutPool() => ExportMany(_freshCsv);

    [Benchmark(Description = "Monthly CSV x 20 sellers, pooled buffers")]
    public long CsvWithPool() => ExportMany(_pooledCsv);

    private long RenderAll(FiscalDocumentPdfRenderer renderer)
    {
        long bytes = 0;
        foreach (var document in _documents)
        {
            bytes += renderer.Render(document).Content.Length;
        }

        return bytes;
    }

    private long ExportMany(MonthlyDeclarationExporter exporter)
    {
        long bytes = 0;
        for (var seller = 0; seller < 20; seller++)
        {
            bytes += exporter.Export(_period, "csv").Value.Content.Length;
        }

        return bytes;
    }

    private static FiscalDocument[] MonthOfInvoices(int count)
    {
        var rates = new TaxRateTable([
            new TaxRate("standard", 0.16m, new DateOnly(2023, 1, 1), null),
            new TaxRate("reduced", 0.05m, new DateOnly(2023, 1, 1), null),
            new TaxRate("exempt", 0m, new DateOnly(2008, 1, 1), "Isento — artigo 9.º do CIVA"),
        ]);
        var kit = FiscalRegimeKit.From(new IvaNormalFactory(rates, new RegimeOptions { LegalNotice = "Preços com IVA incluído." }));
        var issuer = new FiscalParty("400100002", "Grossista da Matola, SA", "Av. das Indústrias, Machava, Matola");
        var now = new DateTimeOffset(2026, 10, 31, 20, 0, 0, TimeSpan.Zero);
        var number = 0;

        return [.. Enumerable.Range(1, count).Select(i =>
        {
            var builder = InvoiceBuilder.For(kit)
                .IssuedBy(issuer)
                .IssuedOn(new DateOnly(2026, 10, 1 + (i % 30)))
                .ForOrder(Guid.NewGuid(), 1000 + i, Guid.NewGuid(), "258841234567");
            for (var line = 0; line < 12; line++)
            {
                builder.AddLine(new LineToPrice($"Produto {line} — saco de 25 kg", $"SKU-{line}", 1 + (line % 3), (line % 3) switch
                {
                    0 => "standard",
                    1 => "reduced",
                    _ => "exempt",
                }, 150m + (line * 37.5m)));
            }

            return (FiscalDocument)builder.Build(() => ++number, now).Value;
        })];
    }
}
