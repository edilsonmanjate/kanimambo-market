using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Invoicing.Application;

public sealed record GetDocument(Guid DocumentId) : IQuery<FiscalDocumentDto>;
