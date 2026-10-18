using Kanimambo.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kanimambo.Shipping.Infrastructure;

internal sealed class ShippingDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ShippingDbContext>
{
    public ShippingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ShippingDbContext>();
        ModulePersistenceExtensions.UseModuleNpgsql(options, "Host=localhost;Database=kanimambo", ShippingDbContext.Schema);
        return new ShippingDbContext(options.Options);
    }
}
