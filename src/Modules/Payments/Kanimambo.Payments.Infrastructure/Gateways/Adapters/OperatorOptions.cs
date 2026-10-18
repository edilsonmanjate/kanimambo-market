namespace Kanimambo.Payments.Infrastructure.Gateways.Adapters;

public sealed class OperatorOptions
{
    public IReadOnlyList<string> Prefixes { get; set; } = [];

    public Uri BaseAddress { get; set; } = new("https+http://mobile-money");

    /// <summary>Shared secret for callback signatures. Local development value only; real ones come from a vault.</summary>
    public string CallbackSecret { get; set; } = string.Empty;
}
