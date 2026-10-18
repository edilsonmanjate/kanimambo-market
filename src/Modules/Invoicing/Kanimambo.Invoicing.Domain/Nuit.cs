using System.Text.RegularExpressions;

namespace Kanimambo.Invoicing.Domain;

/// <summary>Número Único de Identificação Tributária: nine digits. Only the format is checked here.</summary>
public static partial class Nuit
{
    public static bool IsValid(string? value) => value is not null && NuitPattern().IsMatch(value);

    [GeneratedRegex(@"^\d{9}$")]
    private static partial Regex NuitPattern();
}
