namespace Kanimambo.Invoicing.Domain;

/// <summary>Titles, numbering format and the legal mention printed on the document.</summary>
public interface IDocumentFormatter : IRegimeComponent
{
    string Title(DocumentType type);

    string FormatNumber(string series, int year, int number);

    /// <summary>Printed on every document of the regime (for ISPC: why no IVA is charged).</summary>
    string? LegalNotice { get; }
}
