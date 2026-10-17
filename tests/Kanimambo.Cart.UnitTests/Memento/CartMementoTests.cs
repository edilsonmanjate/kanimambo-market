using System.Text.Json;
using Kanimambo.Cart.Domain;

namespace Kanimambo.Cart.UnitTests.Memento;

public sealed class CartMementoTests
{
    private static readonly Guid Arroz = Guid.NewGuid();
    private static readonly Guid Oleo = Guid.NewGuid();
    private static readonly Guid Acucar = Guid.NewGuid();

    private readonly DateTimeOffset _now = new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Every_change_produces_a_new_version_that_can_be_snapshotted()
    {
        var cart = Open();
        var snapshots = new List<CartMemento> { cart.CreateMemento() };

        cart.SetQuantity(Arroz, "ARROZ-25", "Arroz", 1, _now);
        snapshots.Add(cart.CreateMemento());
        cart.SetQuantity(Oleo, "OLEO-5", "Óleo", 2, _now);
        snapshots.Add(cart.CreateMemento());
        cart.ChangeDeliveryCity("Matola", _now);
        snapshots.Add(cart.CreateMemento());

        snapshots.Select(s => s.Version).ShouldBe([1, 2, 3, 4]);
        snapshots.Select(s => s.ItemCount).ShouldBe([0, 1, 3, 3]);
        snapshots[^1].DeliveryCity.ShouldBe("Matola");
    }

    [Fact]
    public void Restoring_snapshot_n_returns_exactly_that_state_and_version()
    {
        var cart = Open();
        cart.SetQuantity(Arroz, "ARROZ-25", "Arroz", 1, _now);
        cart.SetQuantity(Oleo, "OLEO-5", "Óleo", 2, _now);
        var version3 = cart.CreateMemento();
        cart.SetQuantity(Arroz, "ARROZ-25", "Arroz", 5, _now);
        cart.Remove(Oleo, _now);
        cart.SetQuantity(Acucar, "ACUCAR-1", "Açúcar", 10, _now);
        cart.ChangeDeliveryCity("Beira", _now);

        cart.Restore(version3, _now).IsSuccess.ShouldBeTrue();

        cart.Version.ShouldBe(3);
        cart.DeliveryCity.ShouldBe("Maputo");
        cart.Lines.Select(l => (l.Sku, l.Quantity)).ShouldBe([("ARROZ-25", 1), ("OLEO-5", 2)]);
        cart.CreateMemento().ShouldBe(version3 with { TakenAt = cart.UpdatedAt });
    }

    [Fact]
    public void Snapshots_are_immutable_later_changes_never_leak_into_them()
    {
        var cart = Open();
        cart.SetQuantity(Arroz, "ARROZ-25", "Arroz", 1, _now);
        var snapshot = cart.CreateMemento();

        cart.SetQuantity(Arroz, "ARROZ-25", "Arroz", 7, _now);
        cart.SetQuantity(Oleo, "OLEO-5", "Óleo", 1, _now);
        cart.Restore(snapshot, _now);
        cart.SetQuantity(Arroz, "ARROZ-25", "Arroz", 9, _now);

        snapshot.Lines.ShouldHaveSingleItem().Quantity.ShouldBe(1);
        snapshot.Version.ShouldBe(2);
    }

    [Fact]
    public void Snapshots_round_trip_through_json_unchanged()
    {
        var cart = Open();
        cart.SetQuantity(Arroz, "ARROZ-25", "Arroz agulha \"25 kg\"", 2, _now);
        cart.SetQuantity(Acucar, "ACUCAR-1", "Açúcar castanho", 5, _now);
        var snapshot = cart.CreateMemento();

        var json = JsonSerializer.Serialize(snapshot, JsonSerializerOptions.Web);
        var restored = JsonSerializer.Deserialize<CartMemento>(json, JsonSerializerOptions.Web);

        restored.ShouldBe(snapshot);
        json.ShouldContain("\"lines\":[");
    }

    [Fact]
    public void Restoring_never_changes_who_owns_the_cart()
    {
        var cart = ShoppingCart.Open(CartId.New(), new RegisteredCustomer("ana", isBusiness: true), "Matola", _now).Value;
        var snapshot = cart.CreateMemento();

        cart.Restore(snapshot, _now);

        cart.Customer.Id.ShouldBe("ana");
        cart.Customer.Type.ShouldBe(Customer.BusinessType);
    }

    [Fact]
    public void A_snapshot_newer_than_the_cart_cannot_be_applied()
    {
        var other = Open();
        other.SetQuantity(Arroz, "ARROZ-25", "Arroz", 1, _now);
        other.SetQuantity(Arroz, "ARROZ-25", "Arroz", 2, _now);

        Open().Restore(other.CreateMemento(), _now).Error.Code.ShouldBe("cart.snapshot_from_future");
    }

    [Fact]
    public void Quantity_zero_removes_and_limits_are_enforced()
    {
        var cart = Open();
        cart.SetQuantity(Arroz, "ARROZ-25", "Arroz", 3, _now);

        cart.SetQuantity(Arroz, "ARROZ-25", "Arroz", 0, _now);
        cart.Lines.ShouldBeEmpty();

        cart.SetQuantity(Arroz, "ARROZ-25", "Arroz", ShoppingCart.MaxQuantityPerLine + 1, _now).IsFailure.ShouldBeTrue();
        cart.Remove(Oleo, _now).Error.Code.ShouldBe("cart.line_not_found");
    }

    private ShoppingCart Open() => ShoppingCart.Open(CartId.New(), GuestCustomer.Instance, "Maputo", _now).Value;
}
