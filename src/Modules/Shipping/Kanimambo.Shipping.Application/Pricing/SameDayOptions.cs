namespace Kanimambo.Shipping.Application.Pricing;

/// <summary>Same-day delivery by motorbike or txopela inside the Maputo / Matola metropolitan area.</summary>
public sealed class SameDayOptions
{
    /// <summary>Courier that carries same-day parcels (name of a registered courier gateway).</summary>
    public string Courier { get; set; } = string.Empty;

    /// <summary>Cities of the same-day area; both the seller and the customer must be in one of them.</summary>
    public List<string> Cities { get; } = [];

    /// <summary>Orders placed after this local (Maputo) time are delivered the next day.</summary>
    public TimeOnly CutoffLocalTime { get; set; } = new(14, 0);

    /// <summary>Price zones by district (bairro); the fee depends on the customer's district.</summary>
    public List<SameDayZone> Zones { get; } = [];

    /// <summary>Zone used when the customer gives no district.</summary>
    public Dictionary<string, string> DefaultZoneByCity { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Vehicles from smallest to largest: the first one that can carry the weight is used.</summary>
    public List<SameDayVehicle> Vehicles { get; } = [];
}
