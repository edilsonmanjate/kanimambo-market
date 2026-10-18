namespace Kanimambo.Invoicing.Domain;

/// <summary>The numbering series of the regime, per document type (a separate series per regime keeps sequences clean).</summary>
public interface IDocumentSeries : IRegimeComponent
{
    string For(DocumentType type);
}
