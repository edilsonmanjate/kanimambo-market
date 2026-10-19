using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Operations;

namespace Kanimambo.Operations.Application;

/// <summary>Every publishing module's outbox: pending messages, dead letters, oldest pending.</summary>
public sealed record GetOutboxOverview : IQuery<IReadOnlyList<OutboxSummary>>;
