using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Invoicing.Application;

public sealed record GetDocumentPdf(Guid DocumentId) : IQuery<ExportedFile>;
