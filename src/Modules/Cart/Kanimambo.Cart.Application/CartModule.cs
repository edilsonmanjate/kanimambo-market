namespace Kanimambo.Cart.Application;

public static class CartModule
{
    public const string Name = "Cart";

    /// <summary>How many snapshots are kept per cart; older ones are pruned.</summary>
    public const int SnapshotsKept = 20;
}
