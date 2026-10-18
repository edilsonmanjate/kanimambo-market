namespace Kanimambo.SharedKernel.Features;

/// <summary>
/// Feature flags for gradual rollouts (e.g. a new export format). One shared, thread-safe instance for the whole
/// application, provided as a DI singleton — never a static <c>Instance</c>, so tests can swap it.
/// </summary>
public interface IFeatureFlags
{
    /// <summary>False for flags that are not configured: features are off unless switched on.</summary>
    bool IsEnabled(string flag);
}

/// <summary>Flag names, so call sites and configuration cannot drift apart silently.</summary>
public static class FeatureFlagNames
{
    /// <summary>Customers and staff can download invoice PDFs.</summary>
    public const string InvoicePdf = "Invoicing.InvoicePdf";

    /// <summary>The XML variant of the monthly e-Declaração file (CSV and XLSX are always available).</summary>
    public const string EDeclaracaoXml = "Invoicing.EDeclaracaoXml";
}
