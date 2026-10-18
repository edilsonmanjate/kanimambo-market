using Kanimambo.Invoicing.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.Invoicing.Infrastructure;

/// <summary>Fiscal profiles for the Catalog's demo sellers (fictional NUITs), in both regimes.</summary>
public static class InvoicingSeeder
{
    public static readonly (Guid SellerId, string Nuit, string Name, string Address, FiscalRegime Regime)[] Demo =
    [
        (Guid.Parse("01920000-0000-7000-8000-000000000001"), "400100001", "Mercearia Xipamanine, Lda", "Mercado de Xipamanine, Maputo", FiscalRegime.Ispc),
        (Guid.Parse("01920000-0000-7000-8000-000000000002"), "400100002", "Grossista da Matola, SA", "Av. das Indústrias, Machava, Matola", FiscalRegime.IvaNormal),
        (Guid.Parse("01920000-0000-7000-8000-000000000003"), "400100003", "Armazém Chiveve, Lda", "Rua do Chiveve, Beira", FiscalRegime.IvaNormal),
        (Guid.Parse("01920000-0000-7000-8000-000000000004"), "400100004", "Cooperativa do Caju de Nampula", "Av. do Trabalho, Nampula", FiscalRegime.Ispc),
        (Guid.Parse("01920000-0000-7000-8000-000000000005"), "400100005", "Importadora Lebombo, Lda", "Av. Julius Nyerere, Maputo", FiscalRegime.IvaNormal),
        (Guid.Parse("01920000-0000-7000-8000-000000000006"), "400100006", "SolarTech Moçambique, Lda", "Av. Vladimir Lenine, Maputo", FiscalRegime.IvaNormal),
    ];

    public static async Task SeedAsync(DbContext context, bool storeOperationPerformed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var db = (InvoicingDbContext)context;
        if (await db.Taxpayers.AnyAsync(cancellationToken))
        {
            return;
        }

        var at = new DateTimeOffset(2025, 12, 31, 22, 0, 0, TimeSpan.Zero);
        foreach (var (sellerId, nuit, name, address, regime) in Demo)
        {
            db.Taxpayers.Add(TaxpayerProfile.Register(sellerId, nuit, name, address, regime, at).Value);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
