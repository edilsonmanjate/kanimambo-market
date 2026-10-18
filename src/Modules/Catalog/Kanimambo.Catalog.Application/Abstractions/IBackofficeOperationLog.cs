using Kanimambo.Catalog.Domain.Backoffice;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Application.Abstractions;

public interface IBackofficeOperationLog
{
    void Add(BackofficeOperationRecord record);

    Task<BackofficeOperationRecord?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<BackofficeOperationRecord>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);
}
