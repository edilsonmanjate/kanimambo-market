using Kanimambo.Payments.Application.Gateways;
using Kanimambo.Payments.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Kanimambo.Payments.Infrastructure;

internal sealed class KeyedGatewayProvider(IServiceProvider services) : IMobileMoneyGatewayProvider
{
    public IMobileMoneyGateway For(MobileOperator mobileOperator) => services.GetRequiredKeyedService<IMobileMoneyGateway>(mobileOperator);
}
