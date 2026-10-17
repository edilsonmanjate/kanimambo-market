using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.SharedKernel.UnitTests.Messaging;

public sealed class MediatorDispatchTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Send_dispatches_to_the_single_registered_handler()
    {
        await using var host = MediatorHost.Create();

        var result = await host.Mediator.Send(new Ping("maputo"), Ct);

        result.Value.ShouldBe("pong:maputo");
        host.Log.Entries.ShouldBe(["handler"]);
    }

    [Fact]
    public async Task Send_without_a_handler_fails_loudly()
    {
        await using var host = MediatorHost.Create();

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await host.Mediator.Send(new UnhandledQuery(), Ct));

        exception.Message.ShouldContain(nameof(UnhandledQuery));
    }

    [Fact]
    public async Task Publish_runs_every_handler_even_when_one_fails_and_then_reports_the_failure()
    {
        await using var host = MediatorHost.Create();

        var exception = await Should.ThrowAsync<AggregateException>(
            async () => await host.Mediator.Publish(new OrderPaid(Guid.NewGuid()), Ct));

        host.Log.Count("invoice").ShouldBe(1);
        host.Log.Count("failing").ShouldBe(1);
        exception.InnerExceptions.ShouldHaveSingleItem().Message.ShouldBe("SMS gateway down");
    }

    [Fact]
    public async Task Publish_dispatches_on_the_runtime_type_of_the_notification()
    {
        await using var host = MediatorHost.Create();
        INotification notification = new OrderPaid(Guid.NewGuid());

        await Should.ThrowAsync<AggregateException>(async () => await host.Mediator.Publish(notification, Ct));

        host.Log.Count("invoice").ShouldBe(1);
    }

    [Fact]
    public void AddMediator_rejects_types_that_are_not_open_generic_behaviors()
    {
        var options = new MediatorOptions();

        Should.Throw<ArgumentException>(() => options.AddBehavior(typeof(PingHandler)));
        Should.Throw<ArgumentException>(() => options.AddBehavior(typeof(OuterBehavior<Ping, Kanimambo.SharedKernel.Results.Result<string>>)));
    }

    [Fact]
    public void AddMediator_rejects_registering_the_same_behavior_twice()
    {
        var options = new MediatorOptions().AddBehavior(typeof(OuterBehavior<,>));

        Should.Throw<InvalidOperationException>(() => options.AddBehavior(typeof(OuterBehavior<,>)));
    }
}
