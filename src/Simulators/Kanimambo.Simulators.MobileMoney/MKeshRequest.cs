namespace Kanimambo.Simulators.MobileMoney;

public sealed record MKeshRequest(Guid Ref, string Telefone, string Valor, Uri UrlRetorno);
