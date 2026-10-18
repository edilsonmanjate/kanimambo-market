namespace Kanimambo.SharedKernel.Messaging;

/// <summary>Continuation to the next behavior in the pipeline, or to the handler itself.</summary>
public delegate ValueTask<TResponse> RequestHandlerNext<TResponse>();
