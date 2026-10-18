using Kanimambo.SharedKernel.Results;

namespace Kanimambo.SharedKernel.Messaging;

/// <summary>A state-changing request that returns a payload (e.g. the id of what was created).</summary>
public interface ICommand<TValue> : IRequest<Result<TValue>>, ICommandBase;
