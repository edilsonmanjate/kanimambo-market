using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Catalog.Application.Pricing;

public sealed record IvaLine(string Category, decimal Rate, Money Net, Money Iva);
