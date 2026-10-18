using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kanimambo.Api.Idempotency;

/// <summary>Lets <c>dotnet ef</c> build the model without Aspire or a running database.</summary>
internal sealed class IdempotencyDbContextDesignTimeFactory : IDesignTimeDbContextFactory<IdempotencyDbContext>
{
    public IdempotencyDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IdempotencyDbContext>();
        IdempotencyDbContext.Configure(options, "Host=localhost;Database=kanimambo");
        return new IdempotencyDbContext(options.Options);
    }
}
