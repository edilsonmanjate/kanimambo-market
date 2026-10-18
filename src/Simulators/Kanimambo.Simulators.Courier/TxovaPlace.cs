namespace Kanimambo.Simulators.Courier;

// Two deliberately different, simulated courier APIs. The couriers are fictional and the formats are the
// simulator's own contracts; the API's adapters translate each of them into one shared interface.

public sealed record TxovaPlace(string City, string? District, string? Address, string? Phone);
