using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Catalog.Application.Backoffice;

public sealed record SuspendSeller(Guid SellerId, string Reason) : ICommand<BackofficeOperationDto>;
