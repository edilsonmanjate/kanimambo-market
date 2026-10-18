using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Ordering.Application;

/// <summary>Start a recurring B2B order by cloning a previous one (Prototype).</summary>
public sealed record ReorderAsDraft(Guid OrderId) : ICommand<DraftOrderDto>;
