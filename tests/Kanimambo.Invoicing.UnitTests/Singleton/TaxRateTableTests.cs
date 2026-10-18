using Kanimambo.Invoicing.Application.Tax;
using Kanimambo.Invoicing.Contracts;
using Kanimambo.Invoicing.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static Kanimambo.Invoicing.UnitTests.TestFiscal;

namespace Kanimambo.Invoicing.UnitTests.Singleton;

public sealed class TaxRateTableTests
{
    [Theory]
    [InlineData("2022-12-31", 0.17)]
    [InlineData("2023-01-01", 0.16)]
    [InlineData("2026-10-19", 0.16)]
    public void The_rate_in_force_on_the_date_applies(string date, decimal rate) =>
        Rates().RateFor("STANDARD", DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture)).Value.Rate.ShouldBe(rate);

    [Fact]
    public void Unknown_categories_and_dates_before_any_rate_are_errors()
    {
        Rates().RateFor("luxury", Today).Error.Code.ShouldBe("tax.unknown");
        Rates().RateFor("reduced", new DateOnly(2020, 1, 1)).Error.Code.ShouldBe("tax.unknown");
    }

    [Fact]
    public void An_invalid_table_is_refused_and_the_current_one_stays()
    {
        var table = Rates();

        Should.Throw<ArgumentException>(() => table.Load([new TaxRate("standard", 16m, Today, null)]));
        Should.Throw<ArgumentException>(() => table.Load([new TaxRate("exempt", 0m, Today, null)]));

        table.RateFor("standard", Today).Value.Rate.ShouldBe(0.16m);
        table.Version.ShouldBe(1);
    }

    [Fact]
    public void Readers_never_see_a_half_loaded_table_while_it_reloads()
    {
        var table = Rates();
        var failures = 0;

        Parallel.Invoke(
            () =>
            {
                for (var i = 0; i < 2_000; i++)
                {
                    table.Load(i % 2 == 0 ? MozambicanRates : [.. MozambicanRates, new TaxRate("luxury", 0.3m, Today, null)]);
                }
            },
            () => Parallel.For(0, 50_000, _ =>
            {
                if (table.RateFor("standard", Today) is not { IsSuccess: true, Value.Rate: 0.16m })
                {
                    Interlocked.Increment(ref failures);
                }
            }));

        failures.ShouldBe(0);
        table.Version.ShouldBe(2_001);
    }

    [Fact]
    public void One_instance_serves_the_whole_application_and_follows_configuration_reloads()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        var source = new ReloadableSource();
        builder.Configuration.Sources.Add(source);
        source.Set("Invoicing:TaxRates:0:Category", "standard");
        source.Set("Invoicing:TaxRates:0:Rate", "0.16");
        source.Set("Invoicing:TaxRates:0:ValidFrom", "2023-01-01");
        builder.AddInvoicingModule();
        using var services = builder.Services.BuildServiceProvider();

        var table = services.GetRequiredService<TaxRateTable>();
        services.GetRequiredService<ITaxRateTable>().ShouldBeSameAs(table);
        using (var scope = services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITaxRateTable>().ShouldBeSameAs(table);
        }

        source.Set("Invoicing:TaxRates:0:Rate", "0.15");
        source.Reload();

        services.GetRequiredService<ITaxRateTable>().RateFor("standard", Today).Value.Rate.ShouldBe(0.15m);
        services.GetRequiredService<ITaxRateTable>().ShouldBeSameAs(table);
    }
}
