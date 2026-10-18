namespace Kanimambo.SharedKernel.Features;

/// <summary>Flag names, so call sites and configuration cannot drift apart silently.</summary>
public static class FeatureFlagNames
{
    /// <summary>Customers and staff can download invoice PDFs.</summary>
    public const string InvoicePdf = "Invoicing.InvoicePdf";

    /// <summary>The XML variant of the monthly e-Declaração file (CSV and XLSX are always available).</summary>
    public const string EDeclaracaoXml = "Invoicing.EDeclaracaoXml";
}
