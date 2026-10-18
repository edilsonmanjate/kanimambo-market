namespace Kanimambo.SharedKernel.Domain;

/// <summary>Converts amounts from one currency to another at a given rate.</summary>
public interface IExchangeRate
{
    Currency From { get; }

    Currency To { get; }

    decimal Rate { get; }

    Money Convert(Money amount);
}
