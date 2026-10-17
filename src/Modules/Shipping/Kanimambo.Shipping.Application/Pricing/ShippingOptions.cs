namespace Kanimambo.Shipping.Application.Pricing;

/// <summary>
/// Every price, zone, vehicle limit and pickup point comes from configuration (<c>Shipping</c> section): delivery
/// prices change with fuel prices and new zones, and none of it should need a deploy.
/// </summary>
public sealed class ShippingOptions
{
    public const string SectionName = "Shipping";

    /// <summary>Order of preference when checkout does not name a method; the first method that can serve wins.</summary>
    public List<string> DefaultMethods { get; } = [];

    public SameDayOptions SameDay { get; set; } = new();

    public CourierOptions Courier { get; set; } = new();

    public PickupOptions Pickup { get; set; } = new();
}

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

public sealed class SameDayZone
{
    public string Name { get; set; } = string.Empty;

    public decimal FeeMzn { get; set; }

    public List<string> Districts { get; } = [];
}

public sealed class SameDayVehicle
{
    public string Name { get; set; } = string.Empty;

    public decimal MaxKg { get; set; }

    public decimal SurchargeMzn { get; set; }
}

/// <summary>Courier (transportadora) delivery, mainly inter-provincial: priced by destination and weight.</summary>
public sealed class CourierOptions
{
    public string Courier { get; set; } = string.Empty;

    public decimal MaxKg { get; set; } = 500m;

    /// <summary>Weight included in the base fee; every started kilogram above it costs the destination's per-kg rate.</summary>
    public decimal IncludedKg { get; set; } = 5m;

    public Dictionary<string, CourierDestination> Destinations { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class CourierDestination
{
    public decimal BaseFeeMzn { get; set; }

    public decimal PerKgMzn { get; set; }

    public int TransitDays { get; set; }
}

/// <summary>Free collection at marketplace pickup points.</summary>
public sealed class PickupOptions
{
    public List<PickupPoint> Points { get; } = [];
}

public sealed class PickupPoint
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public decimal MaxKg { get; set; } = 30m;
}
