using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Domain.Backoffice;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Application.Backoffice;

public sealed record BackofficeOperationDto(
    Guid Id,
    string Kind,
    string Description,
    string PerformedBy,
    DateTimeOffset PerformedAt,
    string Status,
    string? UndoneBy,
    DateTimeOffset? UndoneAt)
{
    public static BackofficeOperationDto From(BackofficeOperationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new BackofficeOperationDto(record.Id, record.Kind, record.Description, record.PerformedBy, record.PerformedAt,
            record.Status.ToString(), record.UndoneBy, record.UndoneAt);
    }
}

public sealed record AdjustSellerPrices(Guid SellerId, decimal Percentage, string Reason) : ICommand<BackofficeOperationDto>;

public sealed record SuspendSeller(Guid SellerId, string Reason) : ICommand<BackofficeOperationDto>;

public sealed record UndoBackofficeOperation(Guid OperationId) : ICommand<BackofficeOperationDto>;

public sealed record ListBackofficeOperations(int Page = 1, int PageSize = 20) : IQuery<PagedResult<BackofficeOperationDto>>;

public sealed class AdjustSellerPricesValidator : IValidator<AdjustSellerPrices>
{
    public const decimal MinPercentage = -50m;
    public const decimal MaxPercentage = 100m;

    public IEnumerable<FieldError> Validate(AdjustSellerPrices request)
    {
        if (request.Percentage == 0m || request.Percentage < MinPercentage || request.Percentage > MaxPercentage)
        {
            yield return new FieldError(nameof(request.Percentage), $"Percentage must be non-zero, between {MinPercentage} and {MaxPercentage}.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            yield return new FieldError(nameof(request.Reason), "A reason is required for the audit log.");
        }
    }
}

public sealed class SuspendSellerValidator : IValidator<SuspendSeller>
{
    public IEnumerable<FieldError> Validate(SuspendSeller request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            yield return new FieldError(nameof(request.Reason), "A reason is required for the audit log.");
        }
    }
}

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
