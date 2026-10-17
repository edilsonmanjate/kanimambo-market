using Kanimambo.Catalog.Contracts;
using Kanimambo.Ordering.Application;
using Kanimambo.Ordering.Application.Acceptance;
using Kanimambo.Ordering.Domain;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Kanimambo.Ordering.UnitTests.ChainOfResponsibility;

public sealed class AcceptanceChainTests
{
    private static readonly Guid Mercearia = Guid.NewGuid();
    private static readonly Guid Grossista = Guid.NewGuid();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly Calls _calls = new();

    [Fact]
    public async Task The_chain_stops_at_the_first_failing_check()
    {
        var chain = Chain(c => c.Then<Passes1>().Then<FailsA>().Then<FailsB>().Then<Passes2>());

        var result = await chain.AcceptAsync(Context(), Ct);

        result.Error.Code.ShouldBe("test.a");
        _calls.Names.ShouldBe(["Passes1", "FailsA"]);
    }

    [Fact]
    public async Task The_order_of_checks_is_whatever_di_registration_says()
    {
        var chain = Chain(c => c.Then<FailsB>().Then<FailsA>());

        (await chain.AcceptAsync(Context(), Ct)).Error.Code.ShouldBe("test.b");
        chain.Steps.ShouldBe(["FailsB", "FailsA"]);
    }

    [Fact]
    public async Task When_every_check_passes_the_order_is_accepted()
    {
        var chain = Chain(c => c.Then<Passes1>().Then<Passes2>());

        (await chain.AcceptAsync(Context(), Ct)).IsSuccess.ShouldBeTrue();
        _calls.Names.ShouldBe(["Passes1", "Passes2"]);
    }

    [Fact]
    public async Task The_production_chain_runs_the_five_marketplace_checks_in_order()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOrderRepository>(new RecentOrders(0, 0m));
        services.AddSingleton(new AntiFraudSettings(5, 100_000m));
        services.AddSingleton<TimeProvider>(new FakeTimeProvider());
        services.AddOrderingApplication();
        await using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOrderAcceptanceChain>().Steps.ShouldBe([
            nameof(BusinessCustomersNeedValidNuit), nameof(StockMustBeAvailable), nameof(SellersMustDeliverToTheCity),
            nameof(PhoneMustBeWithinFraudLimits), nameof(SellerMinimumsMustBeMet)]);
    }

    [Theory]
    [InlineData("b2b", "", "validation.failed")]
    [InlineData("b2b", "40012345", "validation.failed")]
    [InlineData("b2b", "400123456", null)]
    [InlineData("registered", "", null)]
    public async Task Business_customers_need_a_nine_digit_nuit(string type, string nuit, string? error)
    {
        var result = await Run<BusinessCustomersNeedValidNuit>(Context(customer: new OrderCustomer("x", type, "258841234567", nuit)));

        (result.IsFailure ? result.Error.Code : null).ShouldBe(error);
    }

    [Fact]
    public async Task Stock_must_cover_every_line()
    {
        var result = await Run<StockMustBeAvailable>(Context(lines: [Line(Mercearia, "ARROZ", 5, stock: 3, total: 9_250m)]));

        result.Error.Code.ShouldBe("ordering.insufficient_stock");
        result.Error.Message.ShouldContain("ARROZ (3 left)");
    }

    [Fact]
    public async Task Every_seller_must_deliver_to_the_city()
    {
        var result = await Run<SellersMustDeliverToTheCity>(Context(city: "Beira"));

        result.Error.Code.ShouldBe("ordering.delivery_zone_not_covered");
        result.Error.Message.ShouldContain("Mercearia");
    }

    [Fact]
    public async Task Each_seller_minimum_applies_to_its_own_lines()
    {
        var lines = new[] { Line(Mercearia, "ARROZ", 1, 10, 1_850m), Line(Grossista, "ARROZ-50", 1, 10, 3_500m) };

        var result = await Run<SellerMinimumsMustBeMet>(Context(lines: lines));

        result.Error.Code.ShouldBe("ordering.below_seller_minimum");
        result.Error.Message.ShouldStartWith("Below the seller minimum: Grossista (minimum 5000.00 MZN, have 3500.00 MZN)");
    }

    [Theory]
    [InlineData(4, 1_000, null)]
    [InlineData(5, 1_000, "ordering.fraud_limit_orders")]
    [InlineData(1, 98_000, null)] // exactly at the limit is still allowed
    [InlineData(1, 98_500, "ordering.fraud_limit_value")]
    public async Task One_phone_number_cannot_exceed_the_daily_limits(int recentOrders, decimal recentValue, string? error)
    {
        var check = new PhoneMustBeWithinFraudLimits(new RecentOrders(recentOrders, recentValue), new AntiFraudSettings(5, 100_000m), new FakeTimeProvider());
        var result = await new OrderAcceptanceChain([check]).AcceptAsync(Context(), Ct);

        (result.IsFailure ? result.Error.Code : null).ShouldBe(error);
    }

    private IOrderAcceptanceChain Chain(Action<OrderAcceptanceChainBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_calls);
        services.AddOrderAcceptanceChain(configure);
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IOrderAcceptanceChain>();
    }

    private static Task<Result> Run<T>(OrderAcceptanceContext context)
        where T : OrderAcceptanceHandler, new() =>
        new OrderAcceptanceChain([new T()]).AcceptAsync(context, Ct);

    private static AcceptanceLine Line(Guid seller, string sku, int quantity, int stock, decimal total) =>
        new(Guid.NewGuid(), seller, sku, quantity, stock, Money.Mzn(total));

    private static OrderAcceptanceContext Context(
        OrderCustomer? customer = null,
        string city = "Maputo",
        IReadOnlyList<AcceptanceLine>? lines = null) =>
        new(customer ?? new OrderCustomer("ana", "registered", "258841234567", ""),
            city,
            PaymentMethod.MPesa,
            lines ?? [Line(Mercearia, "ARROZ", 1, 10, 1_850m)],
            new Dictionary<Guid, SellerSnapshot>
            {
                [Mercearia] = new(Mercearia, "Mercearia", true, ["Maputo", "Matola"], Money.Mzn(300m), "Maputo"),
                [Grossista] = new(Grossista, "Grossista", true, ["Matola", "Maputo"], Money.Mzn(5_000m), "Matola"),
            },
            Money.Mzn(2_000m));

    public sealed class Calls
    {
        public List<string> Names { get; } = [];
    }

    public sealed class Passes1(Calls calls) : OrderAcceptanceHandler
    {
        protected override Task<Result> CheckAsync(OrderAcceptanceContext context, CancellationToken cancellationToken)
        {
            calls.Names.Add(Name);
            return Task.FromResult(Result.Success());
        }
    }

    public sealed class Passes2(Calls calls) : OrderAcceptanceHandler
    {
        protected override Task<Result> CheckAsync(OrderAcceptanceContext context, CancellationToken cancellationToken)
        {
            calls.Names.Add(Name);
            return Task.FromResult(Result.Success());
        }
    }

    public sealed class FailsA(Calls calls) : OrderAcceptanceHandler
    {
        protected override Task<Result> CheckAsync(OrderAcceptanceContext context, CancellationToken cancellationToken)
        {
            calls.Names.Add(Name);
            return Task.FromResult(Result.Failure(Error.BusinessRule("test.a", "A failed")));
        }
    }

    public sealed class FailsB(Calls calls) : OrderAcceptanceHandler
    {
        protected override Task<Result> CheckAsync(OrderAcceptanceContext context, CancellationToken cancellationToken)
        {
            calls.Names.Add(Name);
            return Task.FromResult(Result.Failure(Error.BusinessRule("test.b", "B failed")));
        }
    }

    private sealed class RecentOrders(int count, decimal total) : IOrderRepository
    {
        public void Add(Order order) => throw new NotSupportedException();

        public Task<Order?> GetAsync(OrderId id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Order?> FindBySourceAsync(string checkoutSource, Guid sourceId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<(int Count, decimal TotalMzn)> RecentByPhoneAsync(string phone, DateTimeOffset since, CancellationToken cancellationToken) =>
            Task.FromResult((count, total));

        public Task<IReadOnlyList<Order>> ListByCustomerAsync(string customerId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
