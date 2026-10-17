using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Application.Backoffice;
using Kanimambo.Catalog.Domain.Backoffice;
using Kanimambo.Catalog.Domain.Sellers;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Identity;
using Kanimambo.SharedKernel.Results;
using Microsoft.Extensions.Time.Testing;
using static Kanimambo.Catalog.UnitTests.TestCatalog;

namespace Kanimambo.Catalog.UnitTests.Command;

public sealed class BackofficeOperationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 17, 8, 0, 0, TimeSpan.Zero));
    private readonly Seller _seller = Seller();
    private readonly InMemoryCatalogItems _products;
    private readonly InMemoryAuditLog _log = new();
    private readonly Actor _actor = new("ana.backoffice");
    private readonly BackofficeInvoker _invoker;

    public BackofficeOperationTests()
    {
        _products = new InMemoryCatalogItems(Product("ARROZ-25", 1_850m), Product("OLEO-5", 720m), Product("SAL-1", 35m));
        _invoker = new BackofficeInvoker(new BackofficeContext(new InMemorySellers(_seller), _products, _clock), _log, _actor, _clock);
    }

    private IEnumerable<decimal> Prices => _products.GetSimpleProductsAsync(Mercearia, Ct).Result.Select(p => p.UnitPrice.Amount);

    [Fact]
    public async Task Executing_records_who_did_what_and_when()
    {
        var record = (await _invoker.ExecuteAsync(AdjustBy(10m), Ct)).Value;

        Prices.ShouldBe([2_035m, 792m, 38.50m]);
        record.Kind.ShouldBe(AdjustSellerPricesOperation.KindName);
        record.PerformedBy.ShouldBe("ana.backoffice");
        record.PerformedAt.ShouldBe(_clock.GetUtcNow());
        record.Description.ShouldContain("+10%");
        _log.Records.ShouldHaveSingleItem().ShouldBeSameAs(record);
    }

    [Fact]
    public async Task Undo_restores_the_exact_previous_prices_from_the_stored_operation()
    {
        var record = (await _invoker.ExecuteAsync(AdjustBy(-15m), Ct)).Value;
        _clock.Advance(TimeSpan.FromHours(2));
        _actor.Name = "rui.supervisor";

        var undone = await _invoker.UndoAsync(record.Id, Ct);

        undone.IsSuccess.ShouldBeTrue();
        Prices.ShouldBe([1_850m, 720m, 35m]);
        record.Status.ShouldBe(BackofficeOperationStatus.Undone);
        record.UndoneBy.ShouldBe("rui.supervisor");
        record.UndoneAt.ShouldBe(_clock.GetUtcNow());
    }

    [Fact]
    public async Task Undo_refuses_when_a_price_was_changed_after_the_operation()
    {
        var record = (await _invoker.ExecuteAsync(AdjustBy(10m), Ct)).Value;
        var oil = (await _products.GetSimpleProductsAsync(Mercearia, Ct)).Single(p => p.Sku == "OLEO-5");
        oil.ChangePrice(Money.Mzn(999m));

        var undone = await _invoker.UndoAsync(record.Id, Ct);

        undone.Error.Code.ShouldBe("catalog.undo_conflict");
        undone.Error.Message.ShouldContain("OLEO-5");
        Prices.ShouldBe([2_035m, 999m, 38.50m]);
        record.Status.ShouldBe(BackofficeOperationStatus.Executed);
    }

    [Fact]
    public async Task An_operation_can_only_be_undone_once()
    {
        var record = (await _invoker.ExecuteAsync(AdjustBy(10m), Ct)).Value;
        await _invoker.UndoAsync(record.Id, Ct);

        (await _invoker.UndoAsync(record.Id, Ct)).Error.Code.ShouldBe("catalog.operation_already_undone");
    }

    [Fact]
    public async Task Suspending_a_seller_is_undone_by_reactivating_it()
    {
        var record = (await _invoker.ExecuteAsync(new SuspendSellerOperation { SellerId = Mercearia.Value, Reason = "Fraude em investigação" }, Ct)).Value;
        _seller.Status.ShouldBe(SellerStatus.Suspended);
        _seller.DomainEvents.ShouldContain(e => e is SellerSuspended);

        await _invoker.UndoAsync(record.Id, Ct);

        _seller.Status.ShouldBe(SellerStatus.Active);
    }

    [Fact]
    public async Task A_failed_operation_leaves_no_audit_record()
    {
        _seller.Suspend("already", _clock.GetUtcNow());

        var result = await _invoker.ExecuteAsync(new SuspendSellerOperation { SellerId = Mercearia.Value, Reason = "again" }, Ct);

        result.Error.Code.ShouldBe("catalog.seller_already_suspended");
        _log.Records.ShouldBeEmpty();
    }

    [Fact]
    public void Operations_survive_serialization_with_their_undo_state()
    {
        var operation = AdjustBy(5m);
        operation.Changes.Add(new PriceChange(Guid.NewGuid(), "ARROZ-25", 1_850m, 1_942.50m, "MZN"));

        var payload = BackofficeInvoker.Serialize(operation);
        var restored = BackofficeInvoker.Deserialize(payload).ShouldBeOfType<AdjustSellerPricesOperation>();

        payload.ShouldContain($"\"$kind\":\"{AdjustSellerPricesOperation.KindName}\"");
        restored.Percentage.ShouldBe(5m);
        restored.Changes.ShouldBe(operation.Changes);
    }

    private static AdjustSellerPricesOperation AdjustBy(decimal percentage) =>
        new() { SellerId = Mercearia.Value, Percentage = percentage, Reason = "Desvalorização do metical" };

    private sealed class Actor(string name) : ICurrentActor
    {
        public string Name { get; set; } = name;

        public bool IsAuthenticated => true;
    }

    private sealed class InMemoryAuditLog : IBackofficeOperationLog
    {
        public List<BackofficeOperationRecord> Records { get; } = [];

        public void Add(BackofficeOperationRecord record) => Records.Add(record);

        public Task<BackofficeOperationRecord?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Records.Find(r => r.Id == id));

        public Task<PagedResult<BackofficeOperationRecord>> ListAsync(int page, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<BackofficeOperationRecord>(Records, page, pageSize, Records.Count));
    }
}
