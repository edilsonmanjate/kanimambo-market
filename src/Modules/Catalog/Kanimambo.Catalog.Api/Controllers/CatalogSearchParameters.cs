namespace Kanimambo.Catalog.Api.Controllers;

public sealed class CatalogSearchParameters
{
    /// <summary>Text contained in the product name.</summary>
    public string? Q { get; set; }

    public bool InStock { get; set; }

    public decimal? MinPrice { get; set; }

    public decimal? MaxPrice { get; set; }

    public string? Category { get; set; }

    public string? DeliversTo { get; set; }

    public decimal? MinSellerRating { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}
