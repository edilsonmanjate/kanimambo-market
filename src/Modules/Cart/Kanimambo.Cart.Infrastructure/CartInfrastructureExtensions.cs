using Kanimambo.Cart.Application;
using Kanimambo.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kanimambo.Cart.Infrastructure;

public static class CartInfrastructureExtensions
{
    public static IHostApplicationBuilder AddCartModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddModuleDbContext<CartDbContext>(CartModule.Name, CartDbContext.Schema);
        builder.Services.AddScoped<ICartRepository, CartRepository>();
        builder.Services.AddScoped<ICartHistory, CartHistory>();
        builder.Services.AddCartContracts();
        return builder;
    }
}
