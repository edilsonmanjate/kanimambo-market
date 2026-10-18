using Kanimambo.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kanimambo.Invoicing.Infrastructure;

internal sealed class InvoicingDbContextDesignTimeFactory : IDesignTimeDbContextFactory<InvoicingDbContext>
{
    public InvoicingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<InvoicingDbContext>();
        ModulePersistenceExtensions.UseModuleNpgsql(options, "Host=localhost;Database=kanimambo", InvoicingDbContext.Schema);
        return new InvoicingDbContext(options.Options);
    }
}
