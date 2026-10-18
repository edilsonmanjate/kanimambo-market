namespace Kanimambo.Promotions.Application;

public sealed record RuleValidationDto(bool IsValid, string? CanonicalRule, RuleDiagnosticDto? Error);
