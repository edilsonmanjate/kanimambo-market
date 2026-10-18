using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Domain.Backoffice;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Application.Backoffice;

public sealed class BackofficeHandlers(BackofficeInvoker invoker, IBackofficeOperationLog log) :
    IRequestHandler<AdjustSellerPrices, Result<BackofficeOperationDto>>,
    IRequestHandler<SuspendSeller, Result<BackofficeOperationDto>>,
    IRequestHandler<UndoBackofficeOperation, Result<BackofficeOperationDto>>,
    IRequestHandler<ListBackofficeOperations, Result<PagedResult<BackofficeOperationDto>>>
{
    public async ValueTask<Result<BackofficeOperationDto>> Handle(AdjustSellerPrices request, CancellationToken cancellationToken) =>
        Map(await invoker.ExecuteAsync(
            new AdjustSellerPricesOperation { SellerId = request.SellerId, Percentage = request.Percentage, Reason = request.Reason.Trim() },
            cancellationToken));

    public async ValueTask<Result<BackofficeOperationDto>> Handle(SuspendSeller request, CancellationToken cancellationToken) =>
        Map(await invoker.ExecuteAsync(
            new SuspendSellerOperation { SellerId = request.SellerId, Reason = request.Reason.Trim() },
            cancellationToken));

    public async ValueTask<Result<BackofficeOperationDto>> Handle(UndoBackofficeOperation request, CancellationToken cancellationToken) =>
        Map(await invoker.UndoAsync(request.OperationId, cancellationToken));

    public async ValueTask<Result<PagedResult<BackofficeOperationDto>>> Handle(ListBackofficeOperations request, CancellationToken cancellationToken)
    {
        var page = await log.ListAsync(Math.Max(1, request.Page), Math.Clamp(request.PageSize, 1, 100), cancellationToken);
        return new PagedResult<BackofficeOperationDto>([.. page.Items.Select(BackofficeOperationDto.From)], page.Page, page.PageSize, page.TotalCount);
    }

    private static Result<BackofficeOperationDto> Map(Result<BackofficeOperationRecord> result) =>
        result.IsSuccess ? BackofficeOperationDto.From(result.Value) : result.Error;
}
