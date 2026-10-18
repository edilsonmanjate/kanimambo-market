using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Payments.Application.Gateways;

/// <summary>What we ask an operator: "charge this phone this amount, call me back here". <see cref="PaymentId"/> is our reference.</summary>
public sealed record MobileMoneyRequest(Guid PaymentId, string Phone, Money Amount, Uri CallbackUrl, string Description);
