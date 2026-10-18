using Kanimambo.SharedKernel.Results;

namespace Kanimambo.SharedKernel.Messaging;

/// <summary>A state-changing request that returns no payload.</summary>
public interface ICommand : IRequest<Result>, ICommandBase;
