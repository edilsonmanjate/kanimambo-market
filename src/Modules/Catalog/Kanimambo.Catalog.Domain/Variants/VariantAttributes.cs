using System.Globalization;

namespace Kanimambo.Catalog.Domain.Variants;

/// <summary>
/// Flyweight: the intrinsic, immutable part of a sellable variant — packaging, pack size, unit, and
/// optionally size and colour ("saco 25 kg", "caixa 12 un", "bidão 5 l"). Thousands of SKUs share a small
/// set of these, so they are interned by <see cref="VariantAttributesPool"/> instead of duplicated per
/// product. The extrinsic state (SKU, price, stock) stays on the product.
/// </summary>
public sealed class VariantAttributes : IEquatable<VariantAttributes>
{
    internal VariantAttributes(string packaging, decimal packSize, string unit, string? size, string? color)
    {
        Packaging = packaging;
        PackSize = packSize;
        Unit = unit;
        Size = size;
        Color = color;
    }

    /// <summary>How it is packed: <c>saco</c>, <c>caixa</c>, <c>garrafa</c>, <c>bidão</c>, <c>unidade</c>...</summary>
    public string Packaging { get; }

    /// <summary>Quantity inside the packaging, e.g. 25 for a 25 kg bag.</summary>
    public decimal PackSize { get; }

    /// <summary>Unit of <see cref="PackSize"/>: <c>kg</c>, <c>g</c>, <c>l</c>, <c>ml</c>, <c>un</c>.</summary>
    public string Unit { get; }

    public string? Size { get; }

    public string? Color { get; }

    internal VariantKey Key => new(Packaging, PackSize, Unit, Size, Color);

    public string Describe()
    {
        var pack = string.Create(CultureInfo.InvariantCulture, $"{Packaging} {PackSize:0.###} {Unit}");
        return string.Join(" · ", new[] { pack, Size, Color }.Where(p => !string.IsNullOrEmpty(p)));
    }

    /// <summary>Compact persisted form: <c>packaging|pack|unit|size|color</c>.</summary>
    public string ToStorageString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Packaging}|{PackSize:0.####}|{Unit}|{Size}|{Color}");

    public bool Equals(VariantAttributes? other) =>
        other is not null && (ReferenceEquals(this, other) || Key.Equals(other.Key));

    public override bool Equals(object? obj) => obj is VariantAttributes other && Equals(other);

    public override int GetHashCode() => Key.GetHashCode();

    public override string ToString() => Describe();
}

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
