using Kanimambo.Api.IntegrationTests;
using Kanimambo.Api.IntegrationTests.Probes;
using Kanimambo.Testing;
using Microsoft.Extensions.DependencyInjection;

[assembly: AssemblyFixture(typeof(KanimamboApiFactory))]

namespace Kanimambo.Api.IntegrationTests;

/// <summary>Adds test-only probe controllers that exercise cross-cutting HTTP behavior.</summary>
public sealed class KanimamboApiFactory : KanimamboWebApplicationFactory
{
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddSingleton<ProbeState>();
        services.AddControllers().AddApplicationPart(typeof(ProbeController).Assembly);
    }
}
