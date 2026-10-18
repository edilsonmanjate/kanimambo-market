namespace Kanimambo.Ordering.Domain;

/// <summary>Random (v4): guest customers follow their order by id, so it must not be guessable.</summary>
public readonly record struct OrderId(Guid Value)
{
    public static OrderId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
