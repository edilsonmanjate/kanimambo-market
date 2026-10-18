using Kanimambo.SharedKernel.Results;
using Kanimambo.Shipping.Application.Couriers;
using Microsoft.Extensions.DependencyInjection;

namespace Kanimambo.Shipping.Infrastructure;

internal sealed class KeyedCourierProvider(IServiceProvider services) : ICourierGatewayProvider
{
    public Result<ICourierGateway> For(string courier) =>
        services.GetKeyedService<ICourierGateway>(courier.ToUpperInvariant()) is { } gateway
            ? Result.Success(gateway)
            : Error.NotFound("shipping.unknown_courier", $"No courier named '{courier}'.");
}
