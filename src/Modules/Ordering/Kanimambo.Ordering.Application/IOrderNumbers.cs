namespace Kanimambo.Ordering.Application;

public interface IOrderNumbers
{
    Task<long> NextAsync(CancellationToken cancellationToken);
}
