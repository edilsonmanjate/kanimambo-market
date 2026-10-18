using Kanimambo.Payments.Application.Gateways;
using Kanimambo.Payments.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Payments.Application.Processing;

/// <summary>Which phone prefixes belong to which operator (configurable: numbering plans change).</summary>
public sealed class OperatorDirectory(IReadOnlyDictionary<MobileOperator, IReadOnlyCollection<string>> prefixes)
{
    public Result<MobileOperator> OperatorFor(string phone)
    {
        if (phone is not { Length: 12 } || !phone.StartsWith("258", StringComparison.Ordinal))
        {
            return new GatewayError(PaymentFailureReason.InvalidNumber, null, "Phone numbers use the 258XXXXXXXXX format.");
        }

        var prefix = phone.Substring(3, 2);
        foreach (var (mobileOperator, operatorPrefixes) in prefixes)
        {
            if (operatorPrefixes.Contains(prefix))
            {
                return mobileOperator;
            }
        }

        return new GatewayError(PaymentFailureReason.UnknownOperator, prefix,
            $"No mobile-money operator uses the prefix {prefix}. Use an M-Pesa, e-Mola or mKesh number.");
    }
}
