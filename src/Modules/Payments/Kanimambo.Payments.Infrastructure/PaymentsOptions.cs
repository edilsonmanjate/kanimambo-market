using Kanimambo.Payments.Domain;
using Kanimambo.Payments.Infrastructure.Gateways.Adapters;

namespace Kanimambo.Payments.Infrastructure;

public sealed class PaymentsOptions
{
    public const string SectionName = "Payments";

    /// <summary>Public address of this API, used to build the callback URLs given to operators.</summary>
    public Uri CallbackBaseAddress { get; set; } = new("http://localhost:5080/");

    public TimeSpan PinTimeout { get; set; } = TimeSpan.FromSeconds(60);

    public TimeSpan TimeoutCheckInterval { get; set; } = TimeSpan.FromSeconds(10);

    public int GatewayMaxRetries { get; set; } = 2;

    public TimeSpan GatewayRetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(300);

    /// <summary>Per-call timeout towards an operator; a timeout is a transient error and is retried.</summary>
    public TimeSpan GatewayTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public Dictionary<MobileOperator, OperatorOptions> Operators { get; } = [];
}
