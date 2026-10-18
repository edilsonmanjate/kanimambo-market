namespace Kanimambo.Shipping.Application.Pricing;

/// <summary>Courier (transportadora) delivery, mainly inter-provincial: priced by destination and weight.</summary>
public sealed class CourierOptions
{
    public string Courier { get; set; } = string.Empty;

    public decimal MaxKg { get; set; } = 500m;

    /// <summary>Weight included in the base fee; every started kilogram above it costs the destination's per-kg rate.</summary>
    public decimal IncludedKg { get; set; } = 5m;

    public Dictionary<string, CourierDestination> Destinations { get; } = new(StringComparer.OrdinalIgnoreCase);
}
