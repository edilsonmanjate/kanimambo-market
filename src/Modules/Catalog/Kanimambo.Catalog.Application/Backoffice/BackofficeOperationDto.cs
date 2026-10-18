using Kanimambo.Catalog.Domain.Backoffice;

namespace Kanimambo.Catalog.Application.Backoffice;

public sealed record BackofficeOperationDto(
    Guid Id,
    string Kind,
    string Description,
    string PerformedBy,
    DateTimeOffset PerformedAt,
    string Status,
    string? UndoneBy,
    DateTimeOffset? UndoneAt)
{
    public static BackofficeOperationDto From(BackofficeOperationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new BackofficeOperationDto(record.Id, record.Kind, record.Description, record.PerformedBy, record.PerformedAt,
            record.Status.ToString(), record.UndoneBy, record.UndoneAt);
    }
}
