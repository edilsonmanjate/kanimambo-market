using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Promotions.Application;

public sealed record ListRuleVariables : IQuery<IReadOnlyList<RuleVariableDto>>;
