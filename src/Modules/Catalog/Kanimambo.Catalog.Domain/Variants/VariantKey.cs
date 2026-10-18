namespace Kanimambo.Catalog.Domain.Variants;

/// <summary>Allocation-free lookup key (a struct over existing strings), case-insensitive.</summary>
internal readonly record struct VariantKey(string Packaging, decimal PackSize, string Unit, string? Size, string? Color)
{
    public bool Equals(VariantKey other) =>
        PackSize == other.PackSize
        && string.Equals(Packaging, other.Packaging, StringComparison.OrdinalIgnoreCase)
        && string.Equals(Unit, other.Unit, StringComparison.OrdinalIgnoreCase)
        && string.Equals(Size, other.Size, StringComparison.OrdinalIgnoreCase)
        && string.Equals(Color, other.Color, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() => HashCode.Combine(
        StringComparer.OrdinalIgnoreCase.GetHashCode(Packaging),
        PackSize,
        StringComparer.OrdinalIgnoreCase.GetHashCode(Unit),
        Size is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Size),
        Color is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Color));
}
