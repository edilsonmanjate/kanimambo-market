using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Invoicing.Application;

public sealed record ExportMonthlyDeclaration(string IssuerNuit, int Year, int Month, string Format) : IQuery<ExportedFile>;
