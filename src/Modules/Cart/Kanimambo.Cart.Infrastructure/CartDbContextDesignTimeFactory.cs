using Kanimambo.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kanimambo.Cart.Infrastructure;

internal sealed class CartDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CartDbContext>
{
    public CartDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CartDbContext>();
        ModulePersistenceExtensions.UseModuleNpgsql(options, "Host=localhost;Database=kanimambo", CartDbContext.Schema);
        return new CartDbContext(options.Options);
    }
}
