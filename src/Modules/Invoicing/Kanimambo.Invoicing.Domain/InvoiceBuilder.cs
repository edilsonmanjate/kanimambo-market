using System.Globalization;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Invoicing.Domain;

/// <summary>
/// Builder: an invoice has many parts that are each validated against the others — issuer and buyer NUITs, the
/// series and sequential number of the issuer's regime, the issue date, lines priced by the regime's calculator
/// (with the legal reason for every exemption) and totals in MZN. <see cref="Build"/> checks everything and returns
/// <c>Result&lt;Invoice&gt;</c>: an invalid invoice never exists in memory.
/// </summary>
public sealed class InvoiceBuilder
{
    private readonly FiscalRegimeKit _kit;
    private readonly List<LineToPrice> _lines = [];
    private FiscalParty? _issuer;
    private FiscalParty _buyer = FiscalParty.FinalConsumer();
    private bool _buyerNuitRequired;
    private DateOnly? _issueDate;
    private Guid _orderId;
    private long _orderNumber;
    private Guid? _sellerId;
    private string _customerPhone = string.Empty;

    private InvoiceBuilder(FiscalRegimeKit kit) => _kit = kit;

    /// <summary>Starts an invoice of the kit's regime: its calculator, formatter and series are used together.</summary>
    public static InvoiceBuilder For(FiscalRegimeKit kit)
    {
        ArgumentNullException.ThrowIfNull(kit);
        return new InvoiceBuilder(kit);
    }

    public InvoiceBuilder IssuedBy(FiscalParty issuer)
    {
        _issuer = issuer;
        return this;
    }

    public InvoiceBuilder BilledTo(FiscalParty buyer, bool nuitRequired = false)
    {
        ArgumentNullException.ThrowIfNull(buyer);
        _buyer = buyer;
        _buyerNuitRequired = nuitRequired;
        return this;
    }

    public InvoiceBuilder IssuedOn(DateOnly date)
    {
        _issueDate = date;
        return this;
    }

    public InvoiceBuilder ForOrder(Guid orderId, long orderNumber, Guid? sellerId, string customerPhone)
    {
        _orderId = orderId;
        _orderNumber = orderNumber;
        _sellerId = sellerId;
        _customerPhone = customerPhone;
        return this;
    }

    public InvoiceBuilder AddLine(LineToPrice line)
    {
        ArgumentNullException.ThrowIfNull(line);
        _lines.Add(line);
        return this;
    }

    public InvoiceBuilder AddLines(IEnumerable<LineToPrice> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        _lines.AddRange(lines);
        return this;
    }

    /// <summary>
    /// Validates every part and prices every line; all problems are reported at once. Only when everything is valid
    /// is <paramref name="nextNumber"/> called to take the next number of the series — a rejected invoice never
    /// consumes a number, so the series stays without gaps.
    /// </summary>
    public Result<Invoice> Build(Func<int> nextNumber, DateTimeOffset now)
    {
        var errors = new List<FieldError>();

        if (_issuer is null)
        {
            errors.Add(new FieldError("issuer", "An invoice needs an issuer."));
        }
        else if (!Nuit.IsValid(_issuer.Nuit))
        {
            errors.Add(new FieldError("issuer.nuit", $"The issuer's NUIT '{_issuer.Nuit}' must have 9 digits."));
        }

        if (_buyer.Nuit is not null && !Nuit.IsValid(_buyer.Nuit))
        {
            errors.Add(new FieldError("buyer.nuit", $"The buyer's NUIT '{_buyer.Nuit}' must have 9 digits."));
        }
        else if (_buyerNuitRequired && _buyer.Nuit is null)
        {
            errors.Add(new FieldError("buyer.nuit", "Business buyers are invoiced with their NUIT."));
        }

        if (_issueDate is null)
        {
            errors.Add(new FieldError("issueDate", "An invoice needs an issue date."));
        }

        if (_orderId == Guid.Empty)
        {
            errors.Add(new FieldError("order", "An invoice belongs to an order."));
        }

        if (_lines.Count == 0)
        {
            errors.Add(new FieldError("lines", "An invoice needs at least one line."));
        }

        var priced = new List<FiscalLine>(_lines.Count);
        for (var i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            var field = string.Create(CultureInfo.InvariantCulture, $"lines[{i}]");
            if (line.Quantity <= 0)
            {
                errors.Add(new FieldError($"{field}.quantity", "Quantities must be positive."));
                continue;
            }

            if (line.Gross < 0 || line.Discount < 0 || line.Discount > line.Gross)
            {
                errors.Add(new FieldError($"{field}.gross", "Amounts must be positive and the discount cannot exceed the line."));
                continue;
            }

            if (_issueDate is { } on)
            {
                var result = _kit.Calculator.Price(i + 1, line, on);
                if (result.IsFailure)
                {
                    errors.Add(new FieldError($"{field}.taxCategory", result.Error.Message));
                    continue;
                }

                if (result.Value.Rate == 0m && string.IsNullOrWhiteSpace(result.Value.ExemptionReason))
                {
                    errors.Add(new FieldError($"{field}.exemptionReason", $"'{line.Description}' is not taxed: the legal reason must be printed."));
                    continue;
                }

                priced.Add(result.Value);
            }
        }

        if (errors.Count == 0 && priced.Sum(l => l.Gross) <= 0m)
        {
            errors.Add(new FieldError("lines", "The invoice total must be positive."));
        }

        if (errors.Count > 0)
        {
            return Error.Validation(errors);
        }

        ArgumentNullException.ThrowIfNull(nextNumber);
        var number = nextNumber();
        if (number <= 0)
        {
            throw new InvalidOperationException($"The series returned an invalid number ({number}).");
        }

        return new Invoice(_kit, number, _issueDate!.Value, _issuer!, _buyer, _orderId, _orderNumber, _sellerId, _customerPhone, priced, now);
    }
}
