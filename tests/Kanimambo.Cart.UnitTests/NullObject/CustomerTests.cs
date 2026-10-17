using Kanimambo.Cart.Domain;
using Kanimambo.SharedKernel.Identity;

namespace Kanimambo.Cart.UnitTests.NullObject;

public sealed class CustomerTests
{
    [Fact]
    public void Anonymous_shoppers_get_the_guest_null_object()
    {
        var customer = Customer.For(TestActor.Anonymous);

        customer.ShouldBeSameAs(GuestCustomer.Instance);
        customer.Type.ShouldBe("guest");
        customer.Id.ShouldBeEmpty();
    }

    [Fact]
    public void Signed_in_shoppers_are_registered_or_b2b_customers()
    {
        Customer.For(new TestActor("ana")).Type.ShouldBe("registered");
        Customer.For(new TestActor("restaurante-costa-do-sol", KnownRoles.B2B)).Type.ShouldBe("b2b");
    }

    [Fact]
    public void A_guest_cart_is_open_to_whoever_holds_its_id_a_registered_one_only_to_its_owner()
    {
        GuestCustomer.Instance.CanAccess(TestActor.Anonymous).ShouldBeTrue();
        GuestCustomer.Instance.CanAccess(new TestActor("rui")).ShouldBeTrue();

        var ana = new RegisteredCustomer("ana", isBusiness: false);
        ana.CanAccess(new TestActor("ana")).ShouldBeTrue();
        ana.CanAccess(new TestActor("rui")).ShouldBeFalse();
        ana.CanAccess(TestActor.Anonymous).ShouldBeFalse();
    }

    [Fact]
    public void Persisted_carts_without_an_owner_come_back_as_guests()
    {
        Customer.Restore(null, null).ShouldBeSameAs(GuestCustomer.Instance);
        Customer.Restore("ana", Customer.BusinessType).Type.ShouldBe("b2b");
    }
}
