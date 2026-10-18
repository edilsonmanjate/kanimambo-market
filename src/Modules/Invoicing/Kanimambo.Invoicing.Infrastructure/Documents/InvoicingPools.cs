using System.Text;
using Microsoft.Extensions.ObjectPool;

namespace Kanimambo.Invoicing.Infrastructure.Documents;

/// <summary>
/// Object Pool: bulk generation (thousands of PDFs, the monthly AT files of every seller) reuses writers and string
/// builders instead of allocating and collecting them for every document. Pools are singletons.
/// </summary>
public sealed class InvoicingPools(ObjectPool<StringBuilder> stringBuilders, ObjectPool<PdfWriter> pdfWriters)
{
    public ObjectPool<StringBuilder> StringBuilders { get; } = stringBuilders;

    public ObjectPool<PdfWriter> PdfWriters { get; } = pdfWriters;

    public static InvoicingPools Create(ObjectPoolProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return new InvoicingPools(
            provider.CreateStringBuilderPool(initialCapacity: 16 * 1024, maximumRetainedCapacity: 1024 * 1024),
            provider.Create(new DefaultPooledObjectPolicy<PdfWriter>()));
    }

    /// <summary>The same API without reuse — every Get allocates. The baseline of the benchmarks.</summary>
    public static InvoicingPools WithoutPooling() =>
        new(new NoPool<StringBuilder>(() => new StringBuilder(16 * 1024)), new NoPool<PdfWriter>(() => new PdfWriter()));

    private sealed class NoPool<T>(Func<T> create) : ObjectPool<T>
        where T : class
    {
        public override T Get() => create();

        public override void Return(T obj)
        {
        }
    }
}
