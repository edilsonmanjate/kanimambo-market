using System.Globalization;

namespace Kanimambo.Simulators.Wholesaler;

public sealed record WholesalerProductDto(
    string Code,
    string Description,
    string Category,
    string UnitCode,
    long PriceCents,
    string Currency,
    int Stock,
    int WeightGrams,
    string TaxCategory);

public sealed record WholesalerPageDto(int Page, int PageSize, int TotalPages, IReadOnlyList<WholesalerProductDto> Items, int? NextPage);

/// <summary>Deterministic catalog: product families × pack sizes × brands-of-origin, with stable prices and stock.</summary>
public sealed class WholesalerCatalogData
{
    private static readonly (string Name, string Category, string Tax, (string Unit, int Grams, long Cents)[] Packs)[] Families =
    [
        ("Arroz agulha", "mercearia", "exempt", [("SC5KG", 5_000, 42_000), ("SC10KG", 10_000, 82_000), ("SC25KG", 25_000, 185_000), ("SC50KG", 50_000, 350_000)]),
        ("Farinha de milho", "mercearia", "exempt", [("SC5KG", 5_000, 31_000), ("SC10KG", 10_000, 60_000), ("SC25KG", 25_000, 135_000)]),
        ("Açúcar castanho", "mercearia", "standard", [("PC1KG", 1_000, 9_500), ("SC10KG", 10_000, 90_000), ("SC50KG", 50_000, 420_000)]),
        ("Óleo de cozinha", "mercearia", "standard", [("GR1L", 920, 16_000), ("BD5L", 4_600, 72_000), ("BD20L", 18_400, 265_000)]),
        ("Feijão manteiga", "mercearia", "standard", [("PC1KG", 1_000, 16_000), ("SC25KG", 25_000, 380_000)]),
        ("Sabão em barra", "higiene", "standard", [("UN", 200, 4_500), ("CX24", 4_800, 98_000)]),
        ("Água mineral", "bebidas", "standard", [("GR1500ML", 1_500, 3_500), ("CX12", 18_000, 38_000)]),
        ("Massa esparguete", "mercearia", "standard", [("PC500G", 500, 6_500), ("CX20", 10_000, 120_000)]),
    ];

    private static readonly string[] Origins = ["Maputo", "Matola", "Beira", "Chimoio", "Nampula", "Tete", "Quelimane", "Pemba", "Xai-Xai", "Inhambane"];

    private readonly List<WholesalerProductDto> _products;

    public WholesalerCatalogData(int productCount)
    {
        _products = [];
        var index = 0;
        while (_products.Count < productCount)
        {
            var family = Families[index % Families.Length];
            var pack = family.Packs[index / Families.Length % family.Packs.Length];
            var origin = Origins[index / (Families.Length * 4) % Origins.Length];
            var batch = index / (Families.Length * 4 * Origins.Length);

            _products.Add(new WholesalerProductDto(
                string.Create(CultureInfo.InvariantCulture, $"WS-{index + 1:00000}"),
                $"{family.Name} ({origin}) lote {batch + 1}",
                family.Category,
                pack.Unit,
                pack.Cents + (index % 7 * 100),
                "MZN",
                (index * 37) % 500,
                pack.Grams,
                family.Tax));
            index++;
        }
    }

    public int Count => _products.Count;

    public WholesalerPageDto GetPage(int page, int pageSize)
    {
        var totalPages = (int)Math.Ceiling(_products.Count / (double)pageSize);
        var items = _products.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new WholesalerPageDto(page, pageSize, totalPages, items, page < totalPages ? page + 1 : null);
    }
}
