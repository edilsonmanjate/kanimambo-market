namespace Kanimambo.Simulators.MobileMoney;

public sealed class SimulatorOptions
{
    public const string SectionName = "Simulator";

    /// <summary>How long the simulated customer takes to type the PIN.</summary>
    public int ConfirmationDelayMilliseconds { get; set; } = 3_000;

    /// <summary>For the "delayed callback" scenario: longer than the API's PIN timeout, to exercise late confirmations.</summary>
    public int DelayedCallbackMilliseconds { get; set; } = 90_000;

    /// <summary>How many times an unacknowledged callback is sent.</summary>
    public int CallbackAttempts { get; set; } = 3;

    public Dictionary<string, string> Secrets { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MPesa"] = "dev-mpesa-callback-secret",
        ["EMola"] = "dev-emola-callback-secret",
        ["MKesh"] = "dev-mkesh-callback-secret",
    };
}
