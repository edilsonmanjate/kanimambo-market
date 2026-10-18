namespace Kanimambo.Invoicing.Infrastructure;

public sealed class MarketplaceOptions
{
    public string Nuit { get; set; } = string.Empty;

    public string LegalName { get; set; } = string.Empty;

    public string? Address { get; set; }

    public string DeliveryTaxCategory { get; set; } = "standard";

    public string DeliveryDescription { get; set; } = "Serviço de entrega";
}
