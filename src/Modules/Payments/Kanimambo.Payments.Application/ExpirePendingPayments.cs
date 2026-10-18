using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Payments.Application;

/// <summary>Resolves payments whose PIN prompt expired: ask the operator one last time, otherwise time them out.</summary>
public sealed record ExpirePendingPayments(int Max = 100) : ICommand<int>;
