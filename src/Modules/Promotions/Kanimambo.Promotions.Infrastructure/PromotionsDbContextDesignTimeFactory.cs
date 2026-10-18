using Kanimambo.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kanimambo.Promotions.Infrastructure;

internal sealed class PromotionsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<PromotionsDbContext>
{
    public PromotionsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PromotionsDbContext>();
        ModulePersistenceExtensions.UseModuleNpgsql(options, "Host=localhost;Database=kanimambo", PromotionsDbContext.Schema);
        return new PromotionsDbContext(options.Options);
    }
}
