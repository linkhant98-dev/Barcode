namespace EMS.Application.Abstractions;

/// <summary>
/// Section 12.2 - the permission vocabulary. Deliberately coarse (module-level, not per-report-code as the
/// spec's example literally shows) to keep the admin grid manageable; extend with more specific keys
/// (e.g. "Reports.RPT-008.View") if a future pass needs per-report granularity.
/// </summary>
public static class Permissions
{
    public const string SubmitShareholderApplication = "SA.Submit";
    public const string SubmitIssueShares = "IS.Submit";
    public const string SubmitTransferShares = "TS.Submit";
    public const string SubmitBonusShares = "BS.Submit";
    public const string SubmitDividend = "DS.Submit";

    public const string ReportsView = "Reports.View";
    public const string ReportsExport = "Reports.Export";
    public const string ReportsViewSensitiveData = "Reports.ViewSensitiveData";

    public static readonly string[] All =
    [
        SubmitShareholderApplication, SubmitIssueShares, SubmitTransferShares, SubmitBonusShares, SubmitDividend,
        ReportsView, ReportsExport, ReportsViewSensitiveData
    ];
}
