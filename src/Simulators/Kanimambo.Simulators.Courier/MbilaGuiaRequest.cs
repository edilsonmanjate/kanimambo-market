namespace Kanimambo.Simulators.Courier;

public sealed record MbilaGuiaRequest(
    string ReferenciaCliente,
    string Origem,
    string Destino,
    string? Morada,
    string TelefoneDestinatario,
    int PesoGramas,
    string ValorCobranca,
    Uri UrlNotificacao);
