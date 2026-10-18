using System.Globalization;
using System.Text;
using Microsoft.Extensions.ObjectPool;

namespace Kanimambo.Invoicing.Infrastructure.Documents;

/// <summary>
/// A minimal PDF 1.4 writer: A4 pages of text in Helvetica (WinAnsi encoding, so Portuguese accents print). Enough for
/// fiscal documents and with no third-party licence. It is expensive to allocate (content buffer, page list, output
/// stream), so instances are pooled and reset between documents (<see cref="IResettable"/>).
/// </summary>
public sealed class PdfWriter : IResettable, IDisposable
{
    private const float PageWidth = 595f;
    private const float PageHeight = 842f;
    private const float Margin = 50f;

    private static readonly Encoding WinAnsi = Encoding.Latin1;

    private readonly List<string> _pages = [];
    private readonly StringBuilder _content = new(8 * 1024);
    private readonly MemoryStream _output = new(32 * 1024);
    private readonly List<long> _offsets = new(16);
    private float _y;

    public PdfWriter() => StartPage();

    /// <summary>How many documents this instance has rendered; shows reuse in tests and benchmarks.</summary>
    public int Uses { get; private set; }

    public PdfWriter Text(float x, string text, float size = 9f, bool bold = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        _content.Append("BT /").Append(bold ? "F2 " : "F1 ").Append(Number(size)).Append(" Tf ")
            .Append(Number(x)).Append(' ').Append(Number(_y)).Append(" Td (");
        foreach (var c in text)
        {
            _ = c switch
            {
                '(' or ')' or '\\' => _content.Append('\\').Append(c),
                '—' => _content.Append("\\227"),
                '–' => _content.Append("\\226"),
                _ => _content.Append(c <= 'ÿ' ? c : '?'),
            };
        }

        _content.Append(") Tj ET\n");
        return this;
    }

    /// <summary>Moves down one line; starts a new page when the current one is full.</summary>
    public PdfWriter NewLine(float leading = 13f)
    {
        _y -= leading;
        if (_y < Margin)
        {
            EndPage();
            StartPage();
        }

        return this;
    }

    public PdfWriter Rule()
    {
        _content.Append(Number(Margin)).Append(' ').Append(Number(_y + 4)).Append(" m ")
            .Append(Number(PageWidth - Margin)).Append(' ').Append(Number(_y + 4)).Append(" l S\n");
        return NewLine(8f);
    }

    public byte[] ToArray()
    {
        EndPage();
        _output.SetLength(0);
        _offsets.Clear();
        Write("%PDF-1.4\n%âãÏÓ\n");

        // 1 catalog, 2 pages, 3–4 fonts, then (page, content) pairs.
        var pageIds = Enumerable.Range(0, _pages.Count).Select(i => 5 + (i * 2)).ToList();
        Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        Object(2, $"<< /Type /Pages /Count {_pages.Count} /Kids [{string.Join(' ', pageIds.Select(id => $"{id} 0 R"))}] >>");
        Object(3, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        Object(4, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
        for (var i = 0; i < _pages.Count; i++)
        {
            var pageId = pageIds[i];
            Object(pageId, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {Number(PageWidth)} {Number(PageHeight)}] " +
                $"/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {pageId + 1} 0 R >>");
            var stream = _pages[i];
            Object(pageId + 1, $"<< /Length {WinAnsi.GetByteCount(stream)} >>\nstream\n{stream}endstream");
        }

        var xref = _output.Position;
        var table = new StringBuilder().Append("xref\n0 ").Append(_offsets.Count + 1).Append("\n0000000000 65535 f \n");
        foreach (var offset in _offsets)
        {
            table.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }

        Write(table.ToString());
        Write($"trailer\n<< /Size {_offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        Uses++;
        return _output.ToArray();
    }

    /// <summary>Called by the pool when the writer is returned: clears the content, keeps the allocated buffers.</summary>
    public bool TryReset()
    {
        _pages.Clear();
        _content.Clear();
        _output.SetLength(0);
        _offsets.Clear();
        StartPage();
        return _content.Capacity <= 1024 * 1024; // do not keep giant buffers around
    }

    /// <summary>Writers dropped by a full pool are disposed by it (the provider creates a disposable-aware pool).</summary>
    public void Dispose() => _output.Dispose();

    private static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private void StartPage() => _y = PageHeight - Margin;

    private void EndPage()
    {
        if (_content.Length > 0)
        {
            _pages.Add(_content.ToString());
            _content.Clear();
        }
        else if (_pages.Count == 0)
        {
            _pages.Add(string.Empty);
        }
    }

    private void Object(int id, string body)
    {
        while (_offsets.Count < id)
        {
            _offsets.Add(0);
        }

        _offsets[id - 1] = _output.Position;
        Write($"{id} 0 obj\n{body}\nendobj\n");
    }

    private void Write(string text)
    {
        var bytes = WinAnsi.GetBytes(text);
        _output.Write(bytes, 0, bytes.Length);
    }
}
