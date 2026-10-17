using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.SharedKernel.UnitTests.Domain;

public sealed class EntityTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 9, 30, 0, TimeSpan.FromHours(2));

    [Fact]
    public void Entities_with_the_same_type_and_id_are_equal()
    {
        var id = Guid.NewGuid();

        new Seller(id, "Mercearia Avenida").ShouldBe(new Seller(id, "renamed"));
        (new Seller(id, "a") == new Seller(id, "b")).ShouldBeTrue();
    }

    [Fact]
    public void Entities_of_different_types_are_never_equal()
    {
        var id = Guid.NewGuid();

        new Seller(id, "a").Equals(new Customer(id)).ShouldBeFalse();
    }

    [Fact]
    public void Aggregates_collect_domain_events_until_cleared()
    {
        var seller = new Seller(Guid.NewGuid(), "Grossista Beira");

        seller.Suspend(Now);

        var raised = seller.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<SellerSuspended>();
        raised.OccurredAt.ShouldBe(Now);
        raised.EventId.Version.ShouldBe(7);

        seller.ClearDomainEvents();
        seller.DomainEvents.ShouldBeEmpty();
    }

    private sealed record SellerSuspended(Guid SellerId, DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

    private sealed class Seller(Guid id, string name) : AggregateRoot<Guid>(id)
    {
        public string Name { get; } = name;

        public void Suspend(DateTimeOffset now) => Raise(new SellerSuspended(Id, now));
    }

    private sealed class Customer(Guid id) : Entity<Guid>(id);
}
