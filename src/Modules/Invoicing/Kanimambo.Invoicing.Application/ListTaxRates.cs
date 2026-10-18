using Kanimambo.Invoicing.Contracts;
using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Invoicing.Application;

public sealed record ListTaxRates(DateOnly? On) : IQuery<IReadOnlyList<TaxRate>>;
