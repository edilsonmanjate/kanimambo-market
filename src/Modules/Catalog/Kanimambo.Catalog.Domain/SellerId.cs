namespace Kanimambo.Catalog.Domain;

public readonly record struct SellerId(Guid Value)
{
    public static SellerId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
