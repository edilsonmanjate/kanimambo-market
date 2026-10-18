using Kanimambo.Payments.Domain;

namespace Kanimambo.Payments.Application.Processing;

/// <summary>Starts the collection of one payment through a specific channel.</summary>
public interface IPaymentProcessor
{
    string Channel { get; }

    /// <summary>Moves the payment forward (awaiting PIN, confirmed or failed); never throws for business outcomes.</summary>
    Task StartAsync(Payment payment, CancellationToken cancellationToken);
}
