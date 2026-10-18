namespace Kanimambo.Simulators.Courier;

public enum Scenario
{
    /// <summary>Default: collected, (in transit,) delivered.</summary>
    Success,

    /// <summary>Recipient phone ends in 0101: nobody at home, the delivery attempt fails.</summary>
    RecipientAbsent,

    /// <summary>Recipient phone ends in 0102: the courier refuses the booking (does not serve the destination).</summary>
    Refused,

    /// <summary>Recipient phone ends in 0103: the first booking request for a parcel gets a 503; retries succeed.</summary>
    Outage,
}
