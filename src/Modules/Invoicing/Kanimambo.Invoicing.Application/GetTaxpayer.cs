using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Invoicing.Application;

public sealed record GetTaxpayer(Guid SellerId) : IQuery<TaxpayerDto>;
