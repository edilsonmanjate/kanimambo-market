using Kanimambo.SharedKernel.Results;

namespace Kanimambo.SharedKernel.Messaging;

/// <summary>A read-only request. Never runs inside a unit of work.</summary>
public interface IQuery<TValue> : IRequest<Result<TValue>>;
