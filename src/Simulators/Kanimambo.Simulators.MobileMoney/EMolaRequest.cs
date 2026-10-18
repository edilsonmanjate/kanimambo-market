namespace Kanimambo.Simulators.MobileMoney;

public sealed record EMolaRequest(Guid Reference, string Msisdn, long AmountCents, Uri CallbackUrl);
