using System.Reflection;
using Kanimambo.Ordering.Application;
using Kanimambo.Ordering.Application.Checkout;
using Kanimambo.Ordering.Domain;
using Kanimambo.SharedKernel.Domain;
using static Kanimambo.Ordering.UnitTests.TestOrders;

namespace Kanimambo.Ordering.UnitTests.Prototype;

public sealed class ReorderPrototypeTests
{
    [Fact]
    public void A_reorder_is_a_deep_copy_of_the_previous_order()
    {
        var lastWeek = Place(customerType: "b2b");

        var draft = DraftOrder.CloneFrom(lastWeek, Guid.NewGuid(), Now).Value;

        draft.SourceOrderId.ShouldBe(lastWeek.Id.Value);
        draft.CustomerId.ShouldBe(lastWeek.Customer.Id);
        draft.Lines.Select(l => (l.Sku, l.Quantity, l.UnitPrice)).ShouldBe(lastWeek.Lines.Select(l => (l.Sku, l.Quantity, l.UnitPrice)));
        draft.Lines.Zip(lastWeek.Lines).ShouldAllBe(pair => !ReferenceEquals(pair.First, pair.Second));
    }

    [Fact]
    public void Changing_the_clone_never_changes_the_original()
    {
        var lastWeek = Place(customerType: "b2b");
        var oil = lastWeek.Lines.Single(l => l.Sku == "OLEO-5");
        var draft = DraftOrder.CloneFrom(lastWeek, Guid.NewGuid(), Now).Value;

        draft.SetQuantity(oil.ProductId, 10, Now);
        draft.SetQuantity(lastWeek.Lines.Single(l => l.Sku == "ARROZ-25").ProductId, 0, Now);

        draft.Lines.ShouldHaveSingleItem().Quantity.ShouldBe(10);
        draft.Lines[0].LineTotal.ShouldBe(Money.Mzn(7_200m));
        lastWeek.Lines.Count.ShouldBe(2);
        oil.Quantity.ShouldBe(2);
        oil.LineTotal.ShouldBe(Money.Mzn(1_440m));
    }

    [Fact]
    public void A_placed_order_owns_copies_of_the_lines_it_was_placed_with()
    {
        var line = Line("FARINHA-25", 1_350m, 4);

        var order = Place(PaymentMethod.MPesa, "b2b", line);

        order.Lines.ShouldHaveSingleItem().ShouldNotBeSameAs(line);
        order.Lines[0].Quantity.ShouldBe(4);
    }

    [Fact]
    public void Only_business_customers_get_recurring_orders()
    {
        DraftOrder.CloneFrom(Place(customerType: "registered"), Guid.NewGuid(), Now).Error.Code.ShouldBe("ordering.reorder_b2b_only");
    }

    [Fact]
    public void Drafts_snapshot_their_quantities_and_undo_restores_even_removed_lines()
    {
        var lastWeek = Place(customerType: "b2b");
        var arroz = lastWeek.Lines.Single(l => l.Sku == "ARROZ-25").ProductId;
        var draft = DraftOrder.CloneFrom(lastWeek, Guid.NewGuid(), Now).Value;
        var original = draft.CreateMemento();

        draft.SetQuantity(arroz, 0, Now);
        draft.Lines.Count.ShouldBe(1);

        draft.Restore(original, lastWeek, Now).IsSuccess.ShouldBeTrue();

        draft.Version.ShouldBe(1);
        draft.Lines.Select(l => (l.Sku, l.Quantity)).ShouldBe([("ARROZ-25", 1), ("OLEO-5", 2)]);
    }

    [Fact]
    public void A_submitted_draft_is_frozen()
    {
        var lastWeek = Place(customerType: "b2b");
        var draft = DraftOrder.CloneFrom(lastWeek, Guid.NewGuid(), Now).Value;
        draft.MarkSubmitted(Guid.NewGuid(), Now);

        draft.SetQuantity(lastWeek.Lines[0].ProductId, 3, Now).Error.Code.ShouldBe("ordering.draft_submitted");
    }

    [Fact]
    public void The_checkout_handler_depends_on_the_facade_and_nothing_else()
    {
        var constructor = typeof(CheckoutHandlers).GetConstructors().ShouldHaveSingleItem();

        constructor.GetParameters().Select(p => p.ParameterType).ShouldBe([typeof(ICheckoutService)]);
        typeof(ICheckoutService).GetMethods(BindingFlags.Public | BindingFlags.Instance).Length.ShouldBe(2);
    }
}
