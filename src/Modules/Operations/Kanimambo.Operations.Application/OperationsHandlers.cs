using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Operations;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Operations.Application;

public sealed class OperationsHandlers(IEnumerable<IOutboxAdministration> outboxes) :
    IRequestHandler<GetOutboxOverview, Result<IReadOnlyList<OutboxSummary>>>,
    IRequestHandler<ListDeadLetters, Result<IReadOnlyList<DeadLetter>>>,
    IRequestHandler<ReplayDeadLetter, Result>
{
    public async ValueTask<Result<IReadOnlyList<OutboxSummary>>> Handle(GetOutboxOverview request, CancellationToken cancellationToken)
    {
        var summaries = new List<OutboxSummary>();
        foreach (var outbox in outboxes.OrderBy(o => o.Module, StringComparer.Ordinal))
        {
            summaries.Add(await outbox.SummaryAsync(cancellationToken));
        }

        return summaries;
    }

    public async ValueTask<Result<IReadOnlyList<DeadLetter>>> Handle(ListDeadLetters request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Module is { Length: > 0 } module && Find(module) is null)
        {
            return UnknownModule(module);
        }

        var max = Math.Clamp(request.Max, 1, 500);
        var letters = new List<DeadLetter>();
        foreach (var outbox in outboxes.Where(o => request.Module is not { Length: > 0 } || string.Equals(o.Module, request.Module, StringComparison.OrdinalIgnoreCase)))
        {
            letters.AddRange(await outbox.ListDeadLettersAsync(max, cancellationToken));
        }

        return letters.OrderByDescending(l => l.OccurredAt).Take(max).ToList();
    }

    public async ValueTask<Result> Handle(ReplayDeadLetter request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (Find(request.Module) is not { } outbox)
        {
            return UnknownModule(request.Module);
        }

        return await outbox.ReplayAsync(request.MessageId, cancellationToken)
            ? Result.Success()
            : Error.NotFound("operations.dead_letter_not_found", $"No dead letter {request.MessageId} in the {outbox.Module} outbox.");
    }

    private IOutboxAdministration? Find(string module) =>
        outboxes.FirstOrDefault(o => string.Equals(o.Module, module, StringComparison.OrdinalIgnoreCase));

    private Error UnknownModule(string module) =>
        Error.NotFound("operations.unknown_module",
            $"No outbox for module '{module}'. Modules: {string.Join(", ", outboxes.Select(o => o.Module).Order(StringComparer.Ordinal))}.");
}
