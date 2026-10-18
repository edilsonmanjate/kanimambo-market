using Kanimambo.Promotions.Domain.Rules;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Promotions.Domain;

/// <summary>A marketing promotion whose eligibility is a rule written in the promotion rule language.</summary>
public sealed class Promotion : AggregateRoot<Guid>
{
    private CompiledRule? _compiled;

    private Promotion()
    {
    }

    private Promotion(Guid id, string name, string ruleText, CompiledRule rule, PromotionDiscount discount, DateOnly? validFrom, DateOnly? validUntil, string createdBy, DateTimeOffset createdAt)
        : base(id)
    {
        Name = name;
        RuleText = ruleText;
        _compiled = rule;
        Discount = discount;
        ValidFrom = validFrom;
        ValidUntil = validUntil;
        IsActive = true;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>The rule exactly as marketing wrote it; it was parsed and type-checked when the promotion was created.</summary>
    public string RuleText { get; private set; } = string.Empty;

    public PromotionDiscount Discount { get; private set; } = null!;

    public DateOnly? ValidFrom { get; private set; }

    public DateOnly? ValidUntil { get; private set; }

    public bool IsActive { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Parsed lazily after loading from the database.</summary>
    public CompiledRule Rule => _compiled ??= RuleParser.Parse(RuleText).Value;

    public static Result<Promotion> Create(
        string name,
        string rule,
        PromotionDiscount discount,
        DateOnly? validFrom,
        DateOnly? validUntil,
        string createdBy,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(discount);

        var errors = new List<FieldError>();
        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(new FieldError("name", "A promotion needs a name."));
        }

        if (validFrom > validUntil)
        {
            errors.Add(new FieldError("validUntil", "The end date is before the start date."));
        }

        var compiled = RuleParser.Parse(rule);
        if (compiled.IsFailure)
        {
            errors.AddRange(((ValidationError)compiled.Error).Errors);
        }

        if (errors.Count > 0)
        {
            return new ValidationError(errors);
        }

        return new Promotion(Guid.CreateVersion7(now), name.Trim(), rule.Trim(), compiled.Value, discount, validFrom, validUntil, createdBy, now);
    }

    public bool IsValidOn(DateOnly date) =>
        IsActive && (ValidFrom is null || date >= ValidFrom) && (ValidUntil is null || date <= ValidUntil);

    public bool AppliesTo(RuleContext context, DateOnly date) => IsValidOn(date) && Rule.Evaluate(context);

    public Result Deactivate()
    {
        if (!IsActive)
        {
            return Error.Conflict("promotions.already_inactive", $"Promotion '{Name}' is already inactive.");
        }

        IsActive = false;
        return Result.Success();
    }
}
