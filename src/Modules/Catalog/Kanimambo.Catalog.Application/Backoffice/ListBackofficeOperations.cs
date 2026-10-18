using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Application.Backoffice;

public sealed record ListBackofficeOperations(int Page = 1, int PageSize = 20) : IQuery<PagedResult<BackofficeOperationDto>>;
