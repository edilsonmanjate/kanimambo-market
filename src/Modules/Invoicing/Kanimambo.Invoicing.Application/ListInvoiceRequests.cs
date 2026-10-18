using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Invoicing.Application;

public sealed record ListInvoiceRequests(string Status) : IQuery<IReadOnlyList<InvoiceRequestDto>>;
