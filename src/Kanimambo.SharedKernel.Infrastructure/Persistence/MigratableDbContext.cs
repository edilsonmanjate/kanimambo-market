namespace Kanimambo.SharedKernel.Infrastructure.Persistence;

/// <summary>Marks a DbContext whose migrations the host applies at startup.</summary>
public sealed record MigratableDbContext(Type ContextType);
