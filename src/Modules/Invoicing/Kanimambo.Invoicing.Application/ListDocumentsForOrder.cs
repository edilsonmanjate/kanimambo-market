using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Invoicing.Application;

public sealed record ListDocumentsForOrder(Guid OrderId) : IQuery<IReadOnlyList<FiscalDocumentDto>>;
