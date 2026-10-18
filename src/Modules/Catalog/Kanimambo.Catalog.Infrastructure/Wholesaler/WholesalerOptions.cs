namespace Kanimambo.Catalog.Infrastructure.Wholesaler;

public sealed class WholesalerOptions
{
    public const string SectionName = "Catalog:Wholesaler";

    public Uri BaseAddress { get; set; } = new("https+http://wholesaler");

    public int PageSize { get; set; } = 100;
}
