using Kanimambo.Invoicing.Domain;

namespace Kanimambo.Invoicing.Application;

/// <summary>
/// Invoicing settings from configuration. The marketplace itself issues the delivery invoice (it sells the delivery
/// service); <see cref="FallbackTaxCategory"/> applies to a product the catalog no longer knows.
/// </summary>
public sealed record InvoicingSettings(FiscalParty Marketplace, FiscalRegime MarketplaceRegime, string DeliveryTaxCategory, string DeliveryDescription,
    string FallbackTaxCategory);
