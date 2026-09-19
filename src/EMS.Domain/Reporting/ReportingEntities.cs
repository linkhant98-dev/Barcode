using EMS.Domain.Common;

namespace EMS.Domain.Reporting;

/// <summary>13.10 - report catalogue metadata driving the Reports landing page and permission checks.</summary>
public class ReportDefinition
{
    public long ReportDefinitionId { get; set; }
    public string ReportCode { get; set; } = string.Empty; // RPT-001 .. RPT-016
    public string NameEn { get; set; } = string.Empty;
    public string NameMm { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public string RequiredPermission { get; set; } = string.Empty;
    public bool SupportsExcel { get; set; } = true;
    public bool SupportsPdf { get; set; } = true;
    public bool SupportsCsv { get; set; }
}

/// <summary>13.10 / 13.9.4 - one row per report run; stores normalized parameters and links to the generated file.</summary>
public class ReportExecution
{
    public long ReportExecutionId { get; set; }
    public string ReportCode { get; set; } = string.Empty;
    public string RequestedByUserId { get; set; } = string.Empty;
    public string ParametersJson { get; set; } = string.Empty;
    public ReportOutputFormat Format { get; set; }
    public ReportExecutionStatus Status { get; set; } = ReportExecutionStatus.Queued;
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int? RowCount { get; set; }
    public string? FileReference { get; set; }
    public string? Sha256Checksum { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public string? CorrelationId { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>13.6 - optional pre-aggregated dashboard snapshot for heavy widgets.</summary>
public class DashboardSnapshot
{
    public long DashboardSnapshotId { get; set; }
    public string WidgetCode { get; set; } = string.Empty; // DB-01 .. DB-12
    public string FilterDimensionsJson { get; set; } = string.Empty;
    public string DataJson { get; set; } = string.Empty;
    public DateTime DataCutoffUtc { get; set; }
    public string SourceVersion { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }
}

/// <summary>15 - reconciliation exceptions (share ledger vs certificates, paid-up capital, dividend settlement, etc.).</summary>
public class ReconciliationResult : AuditableEntity
{
    public string ControlName { get; set; } = string.Empty;
    public DateOnly BusinessDate { get; set; }
    public decimal ExpectedValue { get; set; }
    public decimal ActualValue { get; set; }
    public decimal Difference { get; set; }
    public string Status { get; set; } = "Open"; // Open, Resolved, Accepted
    public string? Resolution { get; set; }
}
