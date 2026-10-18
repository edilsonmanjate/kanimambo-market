using Kanimambo.SharedKernel.Features;
using Kanimambo.SharedKernel.Infrastructure.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kanimambo.Invoicing.UnitTests.Singleton;

public sealed class FeatureFlagTests
{
    [Fact]
    public void Flags_are_off_unless_configured_and_follow_reloads_on_the_same_instance()
    {
        var source = new ReloadableSource();
        var configuration = new ConfigurationBuilder().Add(source).Build();
        source.Set("Features:Flags:Invoicing.InvoicePdf", "true");
        using var services = new ServiceCollection().AddFeatureFlags(configuration).BuildServiceProvider();

        var flags = services.GetRequiredService<IFeatureFlags>();
        flags.IsEnabled(FeatureFlagNames.InvoicePdf).ShouldBeTrue();
        flags.IsEnabled(FeatureFlagNames.EDeclaracaoXml).ShouldBeFalse();
        flags.IsEnabled("Never.Configured").ShouldBeFalse();

        source.Set("Features:Flags:Invoicing.EDeclaracaoXml", "true");
        source.Reload();

        services.GetRequiredService<IFeatureFlags>().ShouldBeSameAs(flags);
        flags.IsEnabled(FeatureFlagNames.EDeclaracaoXml).ShouldBeTrue();
    }
}
