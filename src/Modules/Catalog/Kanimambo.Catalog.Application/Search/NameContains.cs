using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Kanimambo.SharedKernel.Specifications;

namespace Kanimambo.Catalog.Application.Search;

/// <summary>Case-insensitive search on the product name.</summary>
public sealed class NameContains(string text) : Specification<CatalogEntry>
{
    private readonly string _text = text.Trim().ToLowerInvariant();

    [SuppressMessage("Performance", "CA1862", Justification = "Expression is translated to SQL; StringComparison overloads are not translatable.")]
    public override Expression<Func<CatalogEntry, bool>> ToExpression() => e => e.Name.ToLower().Contains(_text);
}
