using Kanimambo.Cart.Application.Checkout;
using Kanimambo.Cart.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Kanimambo.Cart.Application;

public static class CartApplicationExtensions
{
    /// <summary>Public contracts other modules consume.</summary>
    public static IServiceCollection AddCartContracts(this IServiceCollection services)
    {
        services.AddScoped<ICartReader, CartReader>();
        return services;
    }
}
