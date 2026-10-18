using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Catalog.Application.Imports;

/// <summary>
/// Imports a seller's catalog. Row-level problems are reported and the valid rows are kept; an
/// unreadable source fails the whole command, so the unit of work rolls everything back.
/// </summary>
public sealed record ImportCatalog(Guid SellerId, ImportFormat Format, string SourceName, Stream? Content) : ICommand<ImportReport>;
