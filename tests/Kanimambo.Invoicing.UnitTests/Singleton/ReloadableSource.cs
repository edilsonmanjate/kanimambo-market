using Microsoft.Extensions.Configuration;

namespace Kanimambo.Invoicing.UnitTests.Singleton;

/// <summary>An in-memory configuration source that can signal a reload, like a changed appsettings.json.</summary>
internal sealed class ReloadableSource : ConfigurationProvider, IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder) => this;

    public new void Set(string key, string? value) => Data[key] = value;

    public void Reload() => OnReload();
}
