using ClosedXML.Excel;

namespace Kanimambo.Catalog.Infrastructure.Imports;

internal sealed record ExcelRow(IReadOnlyDictionary<string, XLCellValue> Cells);
