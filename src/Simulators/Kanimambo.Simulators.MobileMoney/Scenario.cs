namespace Kanimambo.Simulators.MobileMoney;

public enum Scenario
{
    /// <summary>Default: the customer confirms after <see cref="SimulatorOptions.ConfirmationDelayMilliseconds"/>.</summary>
    Success,

    /// <summary>Phone ends in 0001: accepted, then fails for insufficient balance once the customer tries.</summary>
    InsufficientBalance,

    /// <summary>Phone ends in 0002: rejected immediately as a duplicate transaction.</summary>
    Duplicate,

    /// <summary>Phone ends in 0003: accepted, the customer never answers (no callback, status stays pending).</summary>
    NoAnswer,

    /// <summary>Phone ends in 0004: success, but the callback arrives after <see cref="SimulatorOptions.DelayedCallbackMilliseconds"/>.</summary>
    DelayedCallback,

    /// <summary>Phone ends in 0005: rejected immediately, the number has no wallet.</summary>
    InvalidNumber,

    /// <summary>Phone ends in 0006: the operator is down, every request gets a 503.</summary>
    Outage,
}
