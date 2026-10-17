using Kanimambo.Ordering.Domain;
using Kanimambo.SharedKernel.Domain;
using static Kanimambo.Ordering.UnitTests.TestOrders;
using A = Kanimambo.Ordering.Domain.OrderAction;
using S = Kanimambo.Ordering.Domain.OrderStatus;

namespace Kanimambo.Ordering.UnitTests.State;

/// <summary>
/// Exhaustive: every reachable state × every action, for prepaid (M-Pesa) and cash-on-delivery orders.
/// Anything not in the table must be refused and leave the order untouched.
/// </summary>
public sealed class OrderStateTests
{
    private static readonly Dictionary<(S, A), S> Prepaid = new()
    {
        [(S.Pending, A.RequestPayment)] = S.AwaitingPayment,
        [(S.Pending, A.Cancel)] = S.Cancelled,
        [(S.AwaitingPayment, A.ConfirmPayment)] = S.Paid,
        [(S.AwaitingPayment, A.FailPayment)] = S.PaymentFailed,
        [(S.AwaitingPayment, A.Cancel)] = S.Cancelled,
        [(S.PaymentFailed, A.RetryPayment)] = S.AwaitingPayment,
        [(S.PaymentFailed, A.Cancel)] = S.Cancelled,
        [(S.PaymentFailed, A.ConfirmPayment)] = S.Paid, // late confirmation after a timeout
        [(S.Paid, A.StartPreparing)] = S.Preparing,
        [(S.Paid, A.Refund)] = S.Refunded,
        [(S.Preparing, A.Dispatch)] = S.OutForDelivery,
        [(S.Preparing, A.Refund)] = S.Refunded,
        [(S.OutForDelivery, A.Deliver)] = S.Delivered,
        [(S.Delivered, A.Refund)] = S.Refunded,
    };

    private static readonly Dictionary<(S, A), S> CashOnDelivery = new()
    {
        [(S.Pending, A.StartPreparing)] = S.Preparing,
        [(S.Pending, A.Cancel)] = S.Cancelled,
        [(S.Preparing, A.Dispatch)] = S.OutForDelivery,
        [(S.Preparing, A.Cancel)] = S.Cancelled,
        [(S.OutForDelivery, A.Deliver)] = S.Delivered,
        [(S.Delivered, A.Refund)] = S.Refunded,
    };

    /// <summary>How to reach each state from a freshly placed order.</summary>
    private static readonly Dictionary<S, A[]> PrepaidPaths = new()
    {
        [S.Pending] = [],
        [S.AwaitingPayment] = [A.RequestPayment],
        [S.PaymentFailed] = [A.RequestPayment, A.FailPayment],
        [S.Paid] = [A.RequestPayment, A.ConfirmPayment],
        [S.Preparing] = [A.RequestPayment, A.ConfirmPayment, A.StartPreparing],
        [S.OutForDelivery] = [A.RequestPayment, A.ConfirmPayment, A.StartPreparing, A.Dispatch],
        [S.Delivered] = [A.RequestPayment, A.ConfirmPayment, A.StartPreparing, A.Dispatch, A.Deliver],
        [S.Cancelled] = [A.Cancel],
        [S.Refunded] = [A.RequestPayment, A.ConfirmPayment, A.Refund],
    };

    private static readonly Dictionary<S, A[]> CashOnDeliveryPaths = new()
    {
        [S.Pending] = [],
        [S.Preparing] = [A.StartPreparing],
        [S.OutForDelivery] = [A.StartPreparing, A.Dispatch],
        [S.Delivered] = [A.StartPreparing, A.Dispatch, A.Deliver],
        [S.Cancelled] = [A.Cancel],
        [S.Refunded] = [A.StartPreparing, A.Dispatch, A.Deliver, A.Refund],
    };

    public static TheoryData<PaymentMethod, S, A> AllCombinations()
    {
        var data = new TheoryData<PaymentMethod, S, A>();
        foreach (var (method, paths) in new[] { (PaymentMethod.MPesa, PrepaidPaths), (PaymentMethod.CashOnDelivery, CashOnDeliveryPaths) })
        {
            foreach (var status in paths.Keys)
            {
                foreach (var action in Enum.GetValues<A>())
                {
                    data.Add(method, status, action);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void Every_state_accepts_exactly_its_transitions(PaymentMethod method, S from, A action)
    {
        var (table, paths) = method == PaymentMethod.CashOnDelivery ? (CashOnDelivery, CashOnDeliveryPaths) : (Prepaid, PrepaidPaths);
        var order = Reach(method, paths[from]);
        order.ClearDomainEvents();

        var result = order.Apply(action);

        if (table.TryGetValue((from, action), out var expected))
        {
            result.IsSuccess.ShouldBeTrue($"{from} --{action}--> {expected} should be allowed ({method})");
            order.Status.ShouldBe(expected);
            order.History[^1].ShouldBe(new OrderStatusChange(from, expected, Now, order.History[^1].Reason));
        }
        else
        {
            result.IsFailure.ShouldBeTrue($"{from} --{action}--> must be impossible ({method})");
            order.Status.ShouldBe(from);
            order.DomainEvents.ShouldBeEmpty();
        }
    }

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void Allowed_actions_match_the_transition_table(PaymentMethod method, S status, A action)
    {
        var (table, paths) = method == PaymentMethod.CashOnDelivery ? (CashOnDelivery, CashOnDeliveryPaths) : (Prepaid, PrepaidPaths);

        Reach(method, paths[status]).AllowedActions.Contains(action).ShouldBe(table.ContainsKey((status, action)));
    }

    [Fact]
    public void Every_status_is_covered_by_the_tests()
    {
        PrepaidPaths.Keys.ShouldBe(Enum.GetValues<S>(), ignoreOrder: true);
    }

    [Fact]
    public void A_prepaid_order_is_paid_when_the_payment_is_confirmed()
    {
        var order = Reach(PaymentMethod.MPesa, [A.RequestPayment]);
        order.ClearDomainEvents();

        order.ConfirmPayment("MP-REF-001", Now);

        order.DomainEvents.OfType<OrderPaid>().ShouldHaveSingleItem().PaymentReference.ShouldBe("MP-REF-001");
        order.PaymentReference.ShouldBe("MP-REF-001");
    }

    [Fact]
    public void A_cash_on_delivery_order_is_paid_when_it_is_delivered()
    {
        var order = Reach(PaymentMethod.CashOnDelivery, [A.StartPreparing, A.Dispatch]);
        order.ClearDomainEvents();

        order.Deliver(Now);

        var paid = order.DomainEvents.OfType<OrderPaid>().ShouldHaveSingleItem();
        paid.PaymentReference.ShouldBe("COD-KM-1001");
        paid.Total.ShouldBe(order.Totals.Total);
    }

    [Fact]
    public void Refused_transitions_explain_themselves()
    {
        var error = Reach(PaymentMethod.MPesa, [A.RequestPayment]).Dispatch(Now).Error;

        error.Code.ShouldBe("ordering.invalid_transition");
        error.Message.ShouldBe("An order that is AwaitingPayment cannot be dispatched.");
    }

    [Fact]
    public void Confirming_a_payment_requires_the_gateway_reference()
    {
        var order = Reach(PaymentMethod.EMola, [A.RequestPayment]);

        order.ConfirmPayment(" ", Now).IsFailure.ShouldBeTrue();
        order.Status.ShouldBe(S.AwaitingPayment);
    }

    [Fact]
    public void Placing_an_order_records_its_totals_and_raises_order_placed()
    {
        var order = Place();

        order.Status.ShouldBe(S.Pending);
        order.Totals.Total.ShouldBe(Money.Mzn(1_850m + 1_440m + 150m));
        order.DomainEvents.OfType<OrderPlaced>().ShouldHaveSingleItem().Total.ShouldBe(order.Totals.Total);
    }

    private static Order Reach(PaymentMethod method, A[] path)
    {
        var order = Place(method);
        foreach (var step in path)
        {
            order.Apply(step).IsSuccess.ShouldBeTrue($"setup step {step} failed");
        }

        return order;
    }
}
