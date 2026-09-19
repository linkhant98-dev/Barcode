namespace EMS.Application.Reporting;

public record ReportColumn(string Key, string Header, bool Numeric = false);

/// <summary>Generic tabular report payload shared by on-screen display, Excel and PDF export (13.9).</summary>
public record ReportResult(
    string ReportCode,
    string Title,
    IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    IReadOnlyDictionary<string, object?>? Totals,
    DateTime GeneratedAtUtc,
    string GeneratedByUserId,
    string FilterSummary);

public record ReportParameters(
    DateOnly? AsOfDate = null,
    string? FinancialYear = null,
    long? ShareholderGroupId = null,
    long? ShareholderId = null,
    long? EventId = null);

/// <summary>Section 13.7/13.8 - the ten baseline reports plus operational/control reports.</summary>
public interface IReportService
{
    Task<IReadOnlyList<(string Code, string Name, string Category)>> GetCatalogueAsync(CancellationToken ct = default);
    Task<ReportResult> RunAsync(string reportCode, ReportParameters parameters, CancellationToken ct = default);
}
