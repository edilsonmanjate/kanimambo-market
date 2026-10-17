using System.Collections.Concurrent;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.SharedKernel.UnitTests.Messaging;

/// <summary>Records what ran, in order, so tests can assert on pipeline composition.</summary>
public sealed class CallLog
{
    private readonly ConcurrentQueue<string> _entries = new();

    public IReadOnlyList<string> Entries => [.. _entries];

    public void Add(string entry) => _entries.Enqueue(entry);

    public int Count(string entry) => _entries.Count(e => e == entry);
}

public sealed record Ping(string Message) : IQuery<string>;

public sealed class PingHandler(CallLog log) : IRequestHandler<Ping, Result<string>>
{
    public ValueTask<Result<string>> Handle(Ping request, CancellationToken cancellationToken)
    {
        log.Add("handler");
        return ValueTask.FromResult<Result<string>>($"pong:{request.Message}");
    }
}

public sealed record CreateStall(string Name, string City) : ICommand<Guid>;

public sealed class CreateStallHandler(CallLog log) : IRequestHandler<CreateStall, Result<Guid>>
{
    public ValueTask<Result<Guid>> Handle(CreateStall request, CancellationToken cancellationToken)
    {
        log.Add("handler");
        return ValueTask.FromResult<Result<Guid>>(Guid.CreateVersion7());
    }
}

public sealed class CreateStallValidator : IValidator<CreateStall>
{
    public IEnumerable<FieldError> Validate(CreateStall request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            yield return new FieldError(nameof(request.Name), "Name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.City))
        {
            yield return new FieldError(nameof(request.City), "City is required.");
        }
    }
}

public sealed record SuspendSeller(Guid SellerId) : ICommand;

public sealed class SuspendSellerHandler(CallLog log) : IRequestHandler<SuspendSeller, Result>
{
    public ValueTask<Result> Handle(SuspendSeller request, CancellationToken cancellationToken)
    {
        log.Add("handler");
        return ValueTask.FromResult<Result>(Error.BusinessRule("catalog.seller_has_open_orders", "Seller has open orders."));
    }
}

public sealed record ConfirmMobileMoneyPayment(Guid IdempotencyKey, bool Succeeds = true) : IIdempotentCommand;

public sealed class ConfirmMobileMoneyPaymentHandler(CallLog log) : IRequestHandler<ConfirmMobileMoneyPayment, Result>
{
    public ValueTask<Result> Handle(ConfirmMobileMoneyPayment request, CancellationToken cancellationToken)
    {
        log.Add("handler");
        return ValueTask.FromResult(request.Succeeds
            ? Result.Success()
            : Result.Failure(Error.Failure("payments.gateway_error", "Gateway error.")));
    }
}

public sealed record UnhandledQuery : IQuery<int>;

public sealed record OrderPaid(Guid OrderId) : INotification;

public sealed class FailingOrderPaidHandler(CallLog log) : INotificationHandler<OrderPaid>
{
    public ValueTask Handle(OrderPaid notification, CancellationToken cancellationToken)
    {
        log.Add("failing");
        throw new InvalidOperationException("SMS gateway down");
    }
}

public sealed class SendInvoiceOnOrderPaid(CallLog log) : INotificationHandler<OrderPaid>
{
    public ValueTask Handle(OrderPaid notification, CancellationToken cancellationToken)
    {
        log.Add("invoice");
        return ValueTask.CompletedTask;
    }
}

public sealed class OuterBehavior<TRequest, TResponse>(CallLog log) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async ValueTask<TResponse> Handle(TRequest request, RequestHandlerNext<TResponse> next, CancellationToken cancellationToken)
    {
        log.Add("outer:before");
        var response = await next();
        log.Add("outer:after");
        return response;
    }
}

public sealed class InnerBehavior<TRequest, TResponse>(CallLog log) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async ValueTask<TResponse> Handle(TRequest request, RequestHandlerNext<TResponse> next, CancellationToken cancellationToken)
    {
        log.Add("inner:before");
        var response = await next();
        log.Add("inner:after");
        return response;
    }
}

/// <summary>In-memory unit of work that records whether it would have committed.</summary>
public sealed class RecordingUnitOfWork(CallLog log) : IUnitOfWork
{
    public async Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> operation, Func<TResult, bool> commitWhen, CancellationToken cancellationToken)
    {
        log.Add("uow:begin");
        var result = await operation();
        log.Add(commitWhen(result) ? "uow:commit" : "uow:rollback");
        return result;
    }
}

public sealed class InMemoryInbox(CallLog log) : IInboxStore
{
    private readonly ConcurrentDictionary<Guid, string> _processed = new();

    public Task<bool> HasBeenProcessedAsync(Guid idempotencyKey, CancellationToken cancellationToken) =>
        Task.FromResult(_processed.ContainsKey(idempotencyKey));

    public void MarkAsProcessed(Guid idempotencyKey, string commandName)
    {
        log.Add("inbox:mark");
        _processed[idempotencyKey] = commandName;
    }
}
