namespace Kanimambo.Invoicing.Application;

public sealed record ExportedFile(string FileName, string ContentType, byte[] Content);
