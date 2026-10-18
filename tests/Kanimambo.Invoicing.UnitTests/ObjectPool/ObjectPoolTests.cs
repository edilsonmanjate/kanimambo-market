using System.Text;
using Kanimambo.Invoicing.Infrastructure.Documents;
using Microsoft.Extensions.ObjectPool;
using static Kanimambo.Invoicing.UnitTests.TestFiscal;

namespace Kanimambo.Invoicing.UnitTests.ObjectPool;

public sealed class ObjectPoolTests
{
    private readonly InvoicingPools _pools = InvoicingPools.Create(new DefaultObjectPoolProvider { MaximumRetained = 2 });

    [Fact]
    public void Sequential_renders_reuse_the_same_writer()
    {
        var renderer = new FiscalDocumentPdfRenderer(_pools);

        for (var i = 1; i <= 20; i++)
        {
            renderer.Render(Invoice(number: i));
        }

        var writer = _pools.PdfWriters.Get();
        writer.Uses.ShouldBe(20);
        _pools.PdfWriters.Return(writer);
    }

    [Fact]
    public void A_reused_writer_starts_clean()
    {
        var renderer = new FiscalDocumentPdfRenderer(_pools);

        var first = Encoding.Latin1.GetString(renderer.Render(Invoice(number: 1)).Content);
        var second = Encoding.Latin1.GetString(renderer.Render(Invoice(IspcKit(), number: 2)).Content);

        first.ShouldContain("FT 2026/000001");
        second.ShouldContain("FS 2026/000002");
        second.ShouldNotContain("FT 2026/000001");
    }

    [Fact]
    public void String_builders_come_back_empty()
    {
        var builder = _pools.StringBuilders.Get();
        builder.Append("NuitEmitente;TipoDocumento");
        _pools.StringBuilders.Return(builder);

        var again = _pools.StringBuilders.Get();
        again.ShouldBeSameAs(builder);
        again.Length.ShouldBe(0);
    }

    [Fact]
    public void Without_pooling_every_render_allocates_a_new_writer()
    {
        var pools = InvoicingPools.WithoutPooling();

        pools.PdfWriters.Get().ShouldNotBeSameAs(pools.PdfWriters.Get());
    }
}
