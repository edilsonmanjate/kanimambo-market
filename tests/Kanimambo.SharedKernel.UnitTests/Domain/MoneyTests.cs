using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.SharedKernel.UnitTests.Domain;

public sealed class MoneyTests
{
    [Theory]
    [InlineData("2.345", "2.35")]
    [InlineData("2.344", "2.34")]
    [InlineData("-2.345", "-2.35")]
    [InlineData("1250", "1250.00")]
    public void Amounts_are_rounded_to_two_places_away_from_zero(string input, string expected)
    {
        Money.Mzn(decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture))
            .Amount.ShouldBe(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Iva_at_16_percent_on_a_mzn_price_is_exact()
    {
        var price = Money.Mzn(1_250m);

        (price * 0.16m).ShouldBe(Money.Mzn(200m));
        (price + price * 0.16m).ShouldBe(Money.Mzn(1_450m));
    }

    [Fact]
    public void Arithmetic_stays_in_decimal()
    {
        (Money.Mzn(0.10m) + Money.Mzn(0.20m)).Amount.ShouldBe(0.30m);
        (Money.Mzn(33.33m) * 3).Amount.ShouldBe(99.99m);
        (Money.Mzn(100m) - Money.Mzn(150m)).IsNegative.ShouldBeTrue();
    }

    [Fact]
    public void Mixing_currencies_is_rejected()
    {
        var mzn = Money.Mzn(100m);
        var usd = Money.Of(100m, Currency.Usd);

        Should.Throw<InvalidOperationException>(() => mzn + usd);
        Should.Throw<InvalidOperationException>(() => mzn > usd);
    }

    [Fact]
    public void Money_is_a_value_object()
    {
        Money.Mzn(10m).ShouldBe(Money.Mzn(10.00m));
        Money.Mzn(10m).GetHashCode().ShouldBe(Money.Mzn(10.00m).GetHashCode());
        Money.Mzn(10m).ShouldNotBe(Money.Of(10m, Currency.Zar));
    }

    [Fact]
    public void Comparison_operators_follow_the_amount()
    {
        (Money.Mzn(2_500m) >= Money.Mzn(2_500m)).ShouldBeTrue();
        (Money.Mzn(2_499.99m) < Money.Mzn(2_500m)).ShouldBeTrue();
    }

    [Fact]
    public void Formats_invariantly_with_the_currency_code()
    {
        Money.Mzn(1_250.5m).ToString().ShouldBe("1250.50 MZN");
    }

    [Theory]
    [InlineData("MZN")]
    [InlineData("usd")]
    [InlineData(" ZAR ")]
    public void Supported_currencies_parse_case_insensitively(string code)
    {
        Currency.FromCode(code).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Unsupported_currency_is_a_validation_error()
    {
        Currency.FromCode("EUR").Error.Type.ShouldBe(Kanimambo.SharedKernel.Results.ErrorType.Validation);
    }
}
