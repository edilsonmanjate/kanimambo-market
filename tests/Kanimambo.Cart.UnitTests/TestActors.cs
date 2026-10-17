using Kanimambo.SharedKernel.Identity;

namespace Kanimambo.Cart.UnitTests;

internal sealed class TestActor(string? name = null, params string[] roles) : ICurrentActor
{
    public static TestActor Anonymous { get; } = new();

    public string Name => name ?? "anonymous";

    public bool IsAuthenticated => name is not null;

    public bool IsInRole(string role) => roles.Contains(role);
}
