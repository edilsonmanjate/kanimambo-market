using Kanimambo.Catalog.Domain;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.Catalog.Domain.Sellers;
using Kanimambo.Catalog.Domain.Variants;
using Kanimambo.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.Catalog.Infrastructure.Persistence;

/// <summary>
/// Demo data: sellers in Maputo, Matola, Beira and Nampula with local products priced in MZN, plus two
/// importers pricing in ZAR and USD. Prices are illustrative. Tax categories are illustrative too: the real
/// classification of each product must follow the IVA code, and the rates come from configuration.
/// </summary>
public static class CatalogSeeder
{
    public static readonly SellerId MerceariaXipamanine = new(Guid.Parse("01920000-0000-7000-8000-000000000001"));
    public static readonly SellerId GrossistaMatola = new(Guid.Parse("01920000-0000-7000-8000-000000000002"));
    public static readonly SellerId ArmazemChiveve = new(Guid.Parse("01920000-0000-7000-8000-000000000003"));
    public static readonly SellerId CooperativaCajuNampula = new(Guid.Parse("01920000-0000-7000-8000-000000000004"));
    public static readonly SellerId ImportadoraLebombo = new(Guid.Parse("01920000-0000-7000-8000-000000000005"));
    public static readonly SellerId SolarTech = new(Guid.Parse("01920000-0000-7000-8000-000000000006"));

    public static readonly IReadOnlySet<Guid> SellerIds = new HashSet<Guid>
    {
        MerceariaXipamanine.Value, GrossistaMatola.Value, ArmazemChiveve.Value, CooperativaCajuNampula.Value, ImportadoraLebombo.Value, SolarTech.Value,
    };

    public static readonly ProductId CabazDoMes = new(Guid.Parse("01920000-0000-7000-9000-0000000000c1"));
    public static readonly ProductId CabazFamilia = new(Guid.Parse("01920000-0000-7000-9000-0000000000c2"));

    public static async Task SeedAsync(DbContext context, bool storeOperationPerformed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var db = (CatalogDbContext)context;

        if (await db.Sellers.AnyAsync(cancellationToken))
        {
            return;
        }

        db.Sellers.AddRange(
            Seller(MerceariaXipamanine, "Mercearia Xipamanine", "Maputo", 4.6m, "Maputo", "Matola"),
            Seller(GrossistaMatola, "Grossista da Matola", "Matola", 4.2m, "Matola", "Maputo", "Beira"),
            Seller(ArmazemChiveve, "Armazém do Chiveve", "Beira", 3.8m, "Beira", "Dondo", "Chimoio"),
            Seller(CooperativaCajuNampula, "Cooperativa de Caju de Nampula", "Nampula", 4.8m, "Nampula", "Nacala", "Maputo"),
            Seller(ImportadoraLebombo, "Importadora Lebombo", "Matola", 4.0m, "Matola", "Maputo"),
            Seller(SolarTech, "SolarTech Moçambique", "Maputo", 4.4m, "Maputo", "Matola", "Beira", "Nampula"));

        var arroz25 = Product(MerceariaXipamanine, "MX-ARROZ-25", "Arroz agulha", "mercearia", "saco", 25, "kg", 1_850m, 40, 25m, "exempt");
        var oleo5 = Product(MerceariaXipamanine, "MX-OLEO-5", "Óleo de cozinha", "mercearia", "bidão", 5, "l", 720m, 60, 4.6m, "standard");
        var acucar1 = Product(MerceariaXipamanine, "MX-ACUCAR-1", "Açúcar castanho", "mercearia", "pacote", 1, "kg", 95m, 300, 1m, "standard");
        var farinha5 = Product(MerceariaXipamanine, "MX-FARINHA-5", "Farinha de milho", "mercearia", "saco", 5, "kg", 310m, 80, 5m, "exempt");
        var feijao1 = Product(MerceariaXipamanine, "MX-FEIJAO-1", "Feijão manteiga", "mercearia", "pacote", 1, "kg", 160m, 200, 1m, "standard");
        var sal1 = Product(MerceariaXipamanine, "MX-SAL-1", "Sal iodado", "mercearia", "pacote", 1, "kg", 35m, 0, 1m, "standard");

        db.CatalogItems.AddRange(arroz25, oleo5, acucar1, farinha5, feijao1, sal1);

        db.CatalogItems.AddRange(
            Product(GrossistaMatola, "GM-ARROZ-50", "Arroz agulha (grossista)", "mercearia", "saco", 50, "kg", 3_500m, 25, 50m, "exempt"),
            Product(GrossistaMatola, "GM-FARINHA-25", "Farinha de milho (grossista)", "mercearia", "saco", 25, "kg", 1_350m, 50, 25m, "exempt"),
            Product(GrossistaMatola, "GM-OLEO-20", "Óleo de cozinha (grossista)", "mercearia", "bidão", 20, "l", 2_650m, 15, 18.4m, "standard"),
            Product(ArmazemChiveve, "AC-PEIXE-1", "Peixe seco", "frescos", "pacote", 1, "kg", 420m, 35, 1m, "standard"),
            Product(ArmazemChiveve, "AC-COCO-UN", "Coco", "frescos", "unidade", 1, "un", 40m, 200, 0.8m, "standard"),
            Product(ArmazemChiveve, "AC-CAPULANA-2M", "Capulana", "vestuário", "unidade", 2, "m", 350m, 70, 0.3m, "standard", color: "azul"),
            Product(CooperativaCajuNampula, "CN-CAJU-1", "Castanha de caju torrada", "mercearia", "pacote", 1, "kg", 950m, 90, 1m, "standard"),
            Product(CooperativaCajuNampula, "CN-CAJU-500", "Castanha de caju torrada", "mercearia", "pacote", 500, "g", 500m, 150, 0.5m, "standard"),
            Product(CooperativaCajuNampula, "CN-PIRIPIRI-200", "Molho de piri-piri", "mercearia", "garrafa", 200, "ml", 120m, 60, 0.25m, "standard"),
            Product(ImportadoraLebombo, "IL-LEITE-12", "Leite UHT", "laticínios", "caixa", 12, "un", 215m, 40, 12.4m, "standard", Currency.Zar),
            Product(ImportadoraLebombo, "IL-MASSA-500", "Massa esparguete", "mercearia", "pacote", 500, "g", 22.9m, 300, 0.5m, "standard", Currency.Zar),
            Product(SolarTech, "ST-LANTERNA", "Lanterna solar", "energia-solar", "unidade", 1, "un", 18.5m, 120, 0.4m, "standard", Currency.Usd),
            Product(SolarTech, "ST-KIT-50W", "Kit solar doméstico 50 W", "energia-solar", "caixa", 1, "un", 120m, 15, 9.5m, "standard", Currency.Usd));

        var cabazDoMes = Bundle.Create(CabazDoMes, MerceariaXipamanine, "MX-CABAZ-MES", "Cabaz do Mês", "cabazes",
            [(arroz25, 1), (oleo5, 1), (acucar1, 5), (farinha5, 2)]).Value;

        // Nested bundle that reuses sugar both directly and through the inner bundle (10 kg in total).
        var cabazFamilia = Bundle.Create(CabazFamilia, MerceariaXipamanine, "MX-CABAZ-FAMILIA", "Cabaz Família", "cabazes",
            [(cabazDoMes, 1), (feijao1, 4), (acucar1, 5)]).Value;

        db.CatalogItems.AddRange(cabazDoMes, cabazFamilia);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static Seller Seller(SellerId id, string name, string city, decimal rating, params string[] deliveryCities) =>
        Domain.Sellers.Seller.Register(id, name, city, rating, deliveryCities).Value;

    private static SimpleProduct Product(
        SellerId seller, string sku, string name, string category,
        string packaging, decimal packSize, string unit,
        decimal price, int stock, decimal weightKg, string taxCategory,
        Currency? currency = null, string? color = null) =>
        SimpleProduct.Create(ProductId.New(), seller, new ProductDetails(
            sku, name, category,
            VariantAttributesPool.Shared.Get(packaging, packSize, unit, color: color).Value,
            Money.Of(price, currency ?? Currency.Mzn),
            stock, weightKg,
            TaxCategory.From(taxCategory).Value)).Value;
}
