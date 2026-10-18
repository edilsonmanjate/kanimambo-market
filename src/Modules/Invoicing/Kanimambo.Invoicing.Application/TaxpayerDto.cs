namespace Kanimambo.Invoicing.Application;

public sealed record TaxpayerDto(Guid SellerId, string Nuit, string LegalName, string? Address, string Regime, int InvoicesIssuedNow);
