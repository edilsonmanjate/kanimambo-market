# Object Pool

## Problem

At month end, Kanimambo generates documents in bulk:

- the PDF of every invoice for sellers who download their month;
- the e-Declaração file of every seller;
- later, notification attachments.

Each PDF needs a sizeable working set: an 8 KB content buffer, a 32 KB output stream and lists of
pages and offsets. Each monthly file needs a growing `StringBuilder` of tens to hundreds of KB.
Allocating those per document and throwing them away produces garbage that survives long enough to be
promoted (Gen1/Gen2), and GC pauses grow exactly when the system is busiest.

## Naive solution

```csharp
foreach (var invoice in month)
{
    var writer = new PdfWriter();          // new buffers every time…
    invoice.Accept(new InvoicePdfVisitor(writer));
    Save(writer.ToArray());                // …garbage right after
}
```

## Solution

Expensive helpers are borrowed from pools (`Microsoft.Extensions.ObjectPool`) and returned after use.

- **`ObjectPool<PdfWriter>`.** `PdfWriter` implements `IResettable`. When a writer is returned, the
  pool calls `TryReset()`, which clears pages and content but keeps the allocated buffers. A writer
  that grew beyond 1 MB is dropped instead of kept. `PdfWriter` is also `IDisposable`, so a full pool
  disposes the surplus writers.
- **`ObjectPool<StringBuilder>`** from `CreateStringBuilderPool(16 KB initial, 1 MB max retained)`, for
  the CSV and XML visitors.
- **Both pools sit in `InvoicingPools`** (a DI singleton), with
  `MaximumRetained = 2 × processor count`.
- **Callers always return in `finally`**, so an exception never leaks a pooled object.
- **The benchmark baseline uses the same API.** `InvoicingPools.WithoutPooling()` exposes the same
  interface but allocates on every `Get`, so the comparison isolates the pooling.

```mermaid
sequenceDiagram
    participant R as FiscalDocumentPdfRenderer
    participant P as ObjectPool<PdfWriter>
    participant W as PdfWriter
    R->>P: Get()
    P-->>R: W (reused, buffers already allocated)
    R->>W: document.Accept(new InvoicePdfVisitor(W)); ToArray()
    R->>P: Return(W)  (finally)
    P->>W: TryReset(): clear content, keep buffers
```

## Benchmark

`dotnet run -c Release --project tests/Kanimambo.Benchmarks -- --filter *ObjectPool* --job short`

The run renders 500 invoices of 12 lines each as PDF, and exports a 500-document monthly CSV 20 times
(one per seller):

| Method | Mean | Gen0 | Gen1 | Gen2 | Allocated |
|---|---:|---:|---:|---:|---:|
| PDFs, new writer per document | 20.81 ms | 7906 | 1313 | – | 47.69 MB |
| **PDFs, pooled writers** | 19.63 ms | 4000 | 156 | – | **24.02 MB** |
| Monthly CSV × 20 sellers, new buffers | 30.74 ms | 5688 | 5656 | 2844 | 31.18 MB |
| **Monthly CSV × 20 sellers, pooled buffers** | 29.23 ms | 2786 | 2786 | 2786 | **25.04 MB** |

*BenchmarkDotNet v0.15.8, .NET 10.0.13, Windows 11, `ShortRun` (3 iterations): the time columns have
wide error margins.*

**Reading the results honestly:**

- **PDFs: allocations halve, and Gen1 collections drop by about 88%.** The pooled buffers stop being
  short-lived garbage that gets promoted.
- **The time saved is within noise.** The win is less GC pressure under bulk load, not raw speed.
- **CSV: about 20% fewer allocations and half the Gen0/Gen1 collections.** What remains is the final
  `string` and `byte[]` of each file, which are the actual output and can't be pooled. That is why
  Gen2 doesn't move: those arrays are large objects.
- **XLSX is not pooled.** The ClosedXML workbook is the expensive part and can't be reset or reused,
  so pooling a buffer next to it would gain nothing.

QR codes on invoices aren't generated yet. When they are, their image buffers are the next candidate
for this pool.

## Where in the code

- [PdfWriter.cs](../../src/Modules/Invoicing/Kanimambo.Invoicing.Infrastructure/Documents/PdfWriter.cs)
  (`PdfWriter : IResettable`, `InvoicingPools`)
- Borrowers: `MonthlyDeclarationExporter` and `FiscalDocumentPdfRenderer` in
  [MonthlyDeclarationExporter.cs](../../src/Modules/Invoicing/Kanimambo.Invoicing.Infrastructure/Documents/MonthlyDeclarationExporter.cs), [FiscalDocumentPdfRenderer.cs](../../src/Modules/Invoicing/Kanimambo.Invoicing.Infrastructure/Documents/FiscalDocumentPdfRenderer.cs)
- Benchmark: [ObjectPoolBenchmarks.cs](../../tests/Kanimambo.Benchmarks/ObjectPool/ObjectPoolBenchmarks.cs)

## Tests

- [ObjectPoolTests](../../tests/Kanimambo.Invoicing.UnitTests/ObjectPool/ObjectPoolTests.cs):
  - 20 sequential renders use **one** writer;
  - a reused writer starts clean (the second PDF has no trace of the first);
  - string builders come back empty and are the same instance;
  - the no-pool variant allocates every time.

## Trade-offs & when NOT to use

- **Measure first.** Pooling is only worth it for objects that are expensive *and* used in volume. The
  benchmark is part of the deliverable for that reason. For one PDF on demand it would be noise.
- **A returned object must be fully reset.** A bug in `TryReset` leaks one customer's invoice into
  another customer's document, which is exactly what the "starts clean" test guards against.
- **Never use an object after returning it.** The `try/finally` pattern keeps borrowing scoped to one
  method.
- **Use `ArrayPool<T>` for raw arrays.** `Microsoft.Extensions.ObjectPool` is for objects with a reset
  policy.
