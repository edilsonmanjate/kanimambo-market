namespace Kanimambo.Invoicing.Application.Regimes;

/// <summary>What differs between regimes on paper: series, legal mention, and why ISPC lines carry no IVA.</summary>
public sealed class RegimeOptions
{
    public string InvoiceSeries { get; set; } = "FT";

    public string CreditNoteSeries { get; set; } = "NC";

    public string? LegalNotice { get; set; }

    /// <summary>ISPC only: the mention on every line, since no IVA is charged.</summary>
    public string NotSubjectReason { get; set; } = "Não sujeito a IVA";
}
