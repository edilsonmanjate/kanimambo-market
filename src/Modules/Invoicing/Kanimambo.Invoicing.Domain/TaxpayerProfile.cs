using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Invoicing.Domain;

/// <summary>A seller's fiscal identity: who issues its invoices, under which regime. Owned by Invoicing, not Catalog.</summary>
public sealed class TaxpayerProfile : AggregateRoot<Guid>
{
    private TaxpayerProfile()
    {
    }

    private TaxpayerProfile(Guid sellerId, string nuit, string legalName, string? address, FiscalRegime regime, DateTimeOffset now)
        : base(sellerId)
    {
        Nuit = nuit;
        LegalName = legalName;
        Address = address;
        Regime = regime;
        UpdatedAt = now;
    }

    public string Nuit { get; private set; } = string.Empty;

    public string LegalName { get; private set; } = string.Empty;

    public string? Address { get; private set; }

    public FiscalRegime Regime { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public FiscalParty AsIssuer => new(Nuit, LegalName, Address);

    public static Result<TaxpayerProfile> Register(Guid sellerId, string nuit, string legalName, string? address, FiscalRegime regime, DateTimeOffset now) =>
        Validate(nuit, legalName) is { } error ? error : new TaxpayerProfile(sellerId, nuit.Trim(), legalName.Trim(), address?.Trim(), regime, now);

    /// <summary>Changes apply to documents issued from now on; issued documents keep the data they were issued with.</summary>
    public Result Update(string nuit, string legalName, string? address, FiscalRegime regime, DateTimeOffset now)
    {
        if (Validate(nuit, legalName) is { } error)
        {
            return error;
        }

        Nuit = nuit.Trim();
        LegalName = legalName.Trim();
        Address = address?.Trim();
        Regime = regime;
        UpdatedAt = now;
        return Result.Success();
    }

    private static ValidationError? Validate(string nuit, string legalName)
    {
        var errors = new List<FieldError>();
        if (!Domain.Nuit.IsValid(nuit?.Trim()))
        {
            errors.Add(new FieldError("nuit", "A NUIT has exactly 9 digits."));
        }

        if (string.IsNullOrWhiteSpace(legalName))
        {
            errors.Add(new FieldError("legalName", "The legal name printed on invoices is required."));
        }

        return errors.Count == 0 ? null : Error.Validation(errors);
    }
}
