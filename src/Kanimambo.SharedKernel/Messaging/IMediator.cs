namespace Kanimambo.SharedKernel.Messaging;

/// <summary>The only dependency controllers need: dispatches commands, queries and notifications in-process.</summary>
public interface IMediator : ISender, IPublisher;
