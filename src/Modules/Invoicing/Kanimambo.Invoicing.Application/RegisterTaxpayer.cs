using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Invoicing.Application;

/// <summary>Register or update a seller's fiscal data; pending invoices of that seller are issued right away.</summary>
public sealed record RegisterTaxpayer(Guid SellerId, string Nuit, string LegalName, string? Address, string Regime) : ICommand<TaxpayerDto>;
