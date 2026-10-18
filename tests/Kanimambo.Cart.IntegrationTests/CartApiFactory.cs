using Kanimambo.Cart.IntegrationTests;
using Kanimambo.Testing;

[assembly: AssemblyFixture(typeof(CartApiFactory))]

namespace Kanimambo.Cart.IntegrationTests;

public sealed class CartApiFactory : KanimamboWebApplicationFactory;
