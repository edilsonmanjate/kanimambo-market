using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Catalog.Application.Products;

public sealed record MoneyDto(decimal Amount, string Currency)
{
    public static MoneyDto From(Money money)
    {
        ArgumentNullException.ThrowIfNull(money);
        return new MoneyDto(money.Amount, money.Currency.Code);
    }
}
