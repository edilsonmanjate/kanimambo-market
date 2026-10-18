namespace Kanimambo.Invoicing.Infrastructure;

public sealed class TaxRateOptions
{
    public string Category { get; set; } = string.Empty;

    public decimal Rate { get; set; }

    public DateOnly ValidFrom { get; set; }

    public string? ExemptionReason { get; set; }
}
