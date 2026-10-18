namespace Kanimambo.Ordering.Domain;

/// <summary>Who placed the order. Guests have an empty <see cref="Id"/> but always a phone (SMS is the main channel).</summary>
public sealed record OrderCustomer(string Id, string Type, string Phone, string Nuit)
{
    public const string BusinessType = "b2b";

    public bool IsBusiness => Type == BusinessType;
}
