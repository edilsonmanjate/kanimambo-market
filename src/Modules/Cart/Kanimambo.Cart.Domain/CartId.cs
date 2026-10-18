namespace Kanimambo.Cart.Domain;

public readonly record struct CartId(Guid Value)
{
    /// <summary>Random (v4) on purpose: a guest cart id is what grants access to it, so it must not be guessable.</summary>
    public static CartId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
