using System.Collections.Concurrent;
using System.Globalization;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Domain.Variants;

/// <summary>
/// Flyweight factory. Returns the single shared <see cref="VariantAttributes"/> instance for a combination,
/// creating it on first use. Lookups use a struct key over the caller's strings, so a hit allocates nothing.
/// The pool grows with the number of distinct combinations (packaging × size × unit × ...), not with the
/// number of SKUs.
/// </summary>
public sealed class VariantAttributesPool
{
    private readonly ConcurrentDictionary<VariantKey, VariantAttributes> _pool = new();

    /// <summary>Process-wide pool, also used by EF Core when materializing products.</summary>
    public static VariantAttributesPool Shared { get; } = new();

    public int Count => _pool.Count;

    public Result<VariantAttributes> Get(string? packaging, decimal packSize, string? unit, string? size = null, string? color = null)
    {
        if (string.IsNullOrWhiteSpace(packaging))
        {
            return Error.Validation(new FieldError("packaging", "Packaging is required (e.g. saco, caixa, unidade)."));
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            return Error.Validation(new FieldError("unit", "Unit is required (e.g. kg, l, un)."));
        }

        if (packSize <= 0m)
        {
            return Error.Validation(new FieldError("packSize", "Pack size must be positive."));
        }

        return Intern(packaging.Trim(), packSize, unit.Trim(), NullIfBlank(size), NullIfBlank(color));
    }

    /// <summary>
    /// Hot path without validation (EF materialization, bulk imports of already-validated data). A hit on an
    /// existing combination allocates nothing: the struct key wraps the caller's strings.
    /// </summary>
    public VariantAttributes Intern(string packaging, decimal packSize, string unit, string? size = null, string? color = null) =>
        _pool.GetOrAdd(new VariantKey(packaging, packSize, unit, size, color), static k => new VariantAttributes(
            k.Packaging.ToLowerInvariant(),
            k.PackSize,
            k.Unit.ToLowerInvariant(),
            k.Size?.ToLowerInvariant(),
            k.Color?.ToLowerInvariant()));

    /// <summary>Parses <see cref="VariantAttributes.ToStorageString"/> back into the shared instance.</summary>
    public VariantAttributes FromStorageString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var parts = value.Split('|');
        if (parts.Length == 5
            && parts[0].Length > 0
            && parts[2].Length > 0
            && decimal.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var packSize)
            && packSize > 0m)
        {
            return Intern(parts[0], packSize, parts[2], NullIfBlank(parts[3]), NullIfBlank(parts[4]));
        }

        throw new FormatException($"Invalid variant attributes '{value}'.");
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
