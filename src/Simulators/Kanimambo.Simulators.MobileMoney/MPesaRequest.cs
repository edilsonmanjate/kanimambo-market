using System.Text.Json.Serialization;

namespace Kanimambo.Simulators.MobileMoney;

// Three deliberately different, simulated wire formats. They are the simulator's own contracts — not the
// operators' official APIs — and the API's adapters translate each of them into one shared interface.

public sealed record MPesaRequest(
    [property: JsonPropertyName("input_TransactionReference")] string TransactionReference,
    [property: JsonPropertyName("input_CustomerMSISDN")] string CustomerMsisdn,
    [property: JsonPropertyName("input_Amount")] string Amount,
    [property: JsonPropertyName("input_ThirdPartyReference")] Guid ThirdPartyReference,
    [property: JsonPropertyName("input_CallbackUrl")] Uri CallbackUrl);
