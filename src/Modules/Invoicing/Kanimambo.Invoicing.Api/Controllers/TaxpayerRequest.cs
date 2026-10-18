namespace Kanimambo.Invoicing.Api.Controllers;

public sealed record TaxpayerRequest(string Nuit, string LegalName, string? Address, string Regime);
