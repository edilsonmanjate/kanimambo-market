using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Promotions.Application;

/// <summary>Dry run for the back-office editor: parse and type-check a rule without saving it.</summary>
public sealed record ValidatePromotionRule(string Rule) : IQuery<RuleValidationDto>;
