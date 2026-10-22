using Kanimambo.Operations.Application;
using Kanimambo.SharedKernel.Infrastructure.Persistence;
using Kanimambo.SharedKernel.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kanimambo.Operations.Infrastructure;

public static class OperationsInfrastructureExtensions
{
    /// <summary>
    /// Operations reads and fixes the other modules' outboxes through <c>IOutboxAdministration</c> (registered by each
    /// publishing module). It has no data of its own, so its commands need no transaction of their own.
    /// </summary>
    public static IHostApplicationBuilder AddOperationsModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddKeyedScoped<IUnitOfWork, PassThroughUnitOfWork>(OperationsModule.Name);
        return builder;
    }
}
