using Kanimambo.Payments.Domain;

namespace Kanimambo.Payments.Application.Gateways;

public interface IMobileMoneyGatewayProvider
{
    /// <summary>The gateway for an operator, with its decorators applied.</summary>
    IMobileMoneyGateway For(MobileOperator mobileOperator);
}
