using Kanimambo.SharedKernel.Messaging.Behaviors;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.SharedKernel.UnitTests.Messaging;

public sealed class PipelineBehaviorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Behaviors_run_in_registration_order_with_the_first_registered_outermost()
    {
        await using var host = MediatorHost.Create(o => o
            .AddBehavior(typeof(OuterBehavior<,>))
            .AddBehavior(typeof(InnerBehavior<,>)));

        await host.Mediator.Send(new Ping("beira"), Ct);

        host.Log.Entries.ShouldBe(["outer:before", "inner:before", "handler", "inner:after", "outer:after"]);
    }

    [Fact]
    public async Task Validation_short_circuits_with_every_field_error_and_never_calls_the_handler()
    {
        await using var host = MediatorHost.Create(o => o.AddBehavior(typeof(ValidationBehavior<,>)));

        var result = await host.Mediator.Send(new CreateStall(Name: "", City: " "), Ct);

        var error = result.Error.ShouldBeOfType<ValidationError>();
        error.Errors.Select(e => e.Field).ShouldBe(["Name", "City"]);
        host.Log.Count("handler").ShouldBe(0);
    }

    [Fact]
    public async Task Validation_lets_valid_requests_through()
    {
        await using var host = MediatorHost.Create(o => o.AddBehavior(typeof(ValidationBehavior<,>)));

        var result = await host.Mediator.Send(new CreateStall("Banca da Dona Rosa", "Nampula"), Ct);

        result.IsSuccess.ShouldBeTrue();
        host.Log.Count("handler").ShouldBe(1);
    }

    [Fact]
    public async Task Unit_of_work_wraps_commands_and_commits_on_success()
    {
        await using var host = MediatorHost.Create(o => o.AddBehavior(typeof(UnitOfWorkBehavior<,>)));

        await host.Mediator.Send(new CreateStall("Loja Xiquitsi", "Matola"), Ct);

        host.Log.Entries.ShouldBe(["uow:begin", "handler", "uow:commit"]);
    }

    [Fact]
    public async Task Unit_of_work_rolls_back_when_the_command_returns_a_failure()
    {
        await using var host = MediatorHost.Create(o => o.AddBehavior(typeof(UnitOfWorkBehavior<,>)));

        var result = await host.Mediator.Send(new SuspendSeller(Guid.NewGuid()), Ct);

        result.Error.Code.ShouldBe("catalog.seller_has_open_orders");
        host.Log.Entries.ShouldBe(["uow:begin", "handler", "uow:rollback"]);
    }

    [Fact]
    public async Task Queries_are_excluded_from_the_unit_of_work_by_generic_constraint()
    {
        await using var host = MediatorHost.Create(o => o.AddBehavior(typeof(UnitOfWorkBehavior<,>)));

        await host.Mediator.Send(new Ping("query"), Ct);

        host.Log.Entries.ShouldBe(["handler"]);
    }

    [Fact]
    public async Task A_command_whose_module_has_no_unit_of_work_is_a_configuration_error()
    {
        await using var host = MediatorHost.Create(o => o.AddBehavior(typeof(UnitOfWorkBehavior<,>)), withModuleServices: false);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await host.Mediator.Send(new CreateStall("x", "y"), Ct));

        exception.Message.ShouldContain(MediatorHost.Module);
    }

    [Fact]
    public async Task Idempotent_command_delivered_twice_runs_its_handler_once()
    {
        await using var host = MediatorHost.Create(o => o
            .AddBehavior(typeof(UnitOfWorkBehavior<,>))
            .AddBehavior(typeof(IdempotencyBehavior<,>)));
        var callback = new ConfirmMobileMoneyPayment(Guid.NewGuid());

        var first = await host.Mediator.Send(callback, Ct);
        var duplicate = await host.Mediator.Send(callback, Ct);

        first.IsSuccess.ShouldBeTrue();
        duplicate.IsSuccess.ShouldBeTrue();
        host.Log.Count("handler").ShouldBe(1);
        host.Log.Entries.ShouldBe(["uow:begin", "handler", "inbox:mark", "uow:commit", "uow:begin", "uow:commit"]);
    }

    [Fact]
    public async Task Failed_idempotent_command_is_not_marked_so_a_redelivery_runs_again()
    {
        await using var host = MediatorHost.Create(o => o.AddBehavior(typeof(IdempotencyBehavior<,>)));
        var key = Guid.NewGuid();

        await host.Mediator.Send(new ConfirmMobileMoneyPayment(key, Succeeds: false), Ct);
        var retry = await host.Mediator.Send(new ConfirmMobileMoneyPayment(key), Ct);

        retry.IsSuccess.ShouldBeTrue();
        host.Log.Count("handler").ShouldBe(2);
        host.Log.Count("inbox:mark").ShouldBe(1);
    }

    [Fact]
    public async Task Idempotency_behavior_ignores_commands_that_are_not_idempotent()
    {
        await using var host = MediatorHost.Create(o => o.AddBehavior(typeof(IdempotencyBehavior<,>)));

        await host.Mediator.Send(new CreateStall("a", "b"), Ct);
        await host.Mediator.Send(new CreateStall("a", "b"), Ct);

        host.Log.Count("handler").ShouldBe(2);
        host.Log.Count("inbox:mark").ShouldBe(0);
    }

    [Fact]
    public async Task Logging_behavior_is_transparent_to_the_response()
    {
        await using var host = MediatorHost.Create(o => o.AddBehavior(typeof(LoggingBehavior<,>)));

        var success = await host.Mediator.Send(new Ping("nacala"), Ct);
        var failure = await host.Mediator.Send(new SuspendSeller(Guid.NewGuid()), Ct);

        success.Value.ShouldBe("pong:nacala");
        failure.IsFailure.ShouldBeTrue();
    }
}
