using System.Xml.Linq;

namespace Kanimambo.Catalog.Infrastructure.Imports;

internal sealed record XmlArticle(XElement Element, string? Currency);
