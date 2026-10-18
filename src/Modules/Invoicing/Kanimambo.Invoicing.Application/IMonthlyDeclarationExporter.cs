using Kanimambo.Invoicing.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Invoicing.Application;

/// <summary>Monthly file for the AT's e-Declaração portal, one visitor per format.</summary>
public interface IMonthlyDeclarationExporter
{
    IReadOnlyCollection<string> Formats { get; }

    Result<ExportedFile> Export(FiscalPeriod period, string format);
}
