using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Catalog.Application.Backoffice;

public sealed record UndoBackofficeOperation(Guid OperationId) : ICommand<BackofficeOperationDto>;
