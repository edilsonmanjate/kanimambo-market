using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Shipping.Application.Couriers;

public interface ICourierGatewayProvider
{
    Result<ICourierGateway> For(string courier);
}
