namespace Kanimambo.Invoicing.Application;

public sealed record InvoiceRequestDto(Guid Id, Guid OrderId, long OrderNumber, Guid SellerId, string Status, Guid? InvoiceId, string? Problem);
