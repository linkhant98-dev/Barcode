namespace EMS.Application.Reporting;

public record ShareholdingByGroupItem(string GroupName, decimal Shares, int HolderCount, decimal Percentage);

public record YearlyCapitalItem(string FinancialYear, decimal ClosingPaidUpCapital);

public record YearlyDividendItem(string FinancialYear, decimal DividendPercentage, decimal ProvisionAmount);

public record DividendComparisonItem(string FinancialYear, decimal CashWithdrawal, decimal AccountTransfer, decimal Reinvested, decimal Outstanding);

public record DashboardKpis(
    int RegisteredShareholders,
    decimal TotalShares,
    decimal PaidUpCapital,
    int PendingApprovals,
    decimal DividendProvisionCurrentYear,
    int CertificatesOnIssue,
    int ApplicationsInProgress);

public record PendingApprovalItem(string Reference, string Module, string SubmittedBy, DateTime SubmittedAtUtc, string StepName, long ApprovalStepId);

public record RecentTransactionItem(string Reference, string Module, string Status, DateTime PostedAtUtc);

public record GroupFilterOption(long Id, string NameEn);

public record DashboardViewData(
    DateTime DataAsOfUtc,
    DashboardKpis Kpis,
    IReadOnlyList<ShareholdingByGroupItem> ShareholdingByGroup,
    IReadOnlyList<YearlyCapitalItem> YearlyPaidUpCapital,
    IReadOnlyList<YearlyDividendItem> YearlyDividend,
    IReadOnlyList<DividendComparisonItem> DividendComparison,
    IReadOnlyList<PendingApprovalItem> MyPendingApprovals,
    IReadOnlyList<RecentTransactionItem> RecentTransactions,
    int PendingKycCount,
    int DataQualityExceptionCount,
    /// <summary>3.1 - every active shareholder group, for the dashboard's group filter select.</summary>
    IReadOnlyList<GroupFilterOption> AvailableGroups,
    long? SelectedGroupId,
    string? SelectedGroupName);

/// <summary>Section 13.2-13.6 - the dashboard landing page: five baseline analytics plus operational widgets.</summary>
public interface IDashboardService
{
    /// <summary>
    /// <paramref name="groupFilterId"/> (3.1) scopes RegisteredShareholders/TotalShares/PaidUpCapital/
    /// CertificatesOnIssue to one shareholder group - both directly (Shareholder/ShareCertificate carry a
    /// ShareholderId) so the join is exact, unlike a name-based approximation. ShareholdingByGroup (DB-01/02,
    /// whose whole purpose is comparing groups) and the company-wide corporate actions (DB-03/04/05, dividend
    /// provision, pending approvals, applications in progress - which precede group assignment, per 4.1.1)
    /// are never filtered.
    /// </summary>
    Task<DashboardViewData> GetDashboardAsync(string? currentUserId, DateOnly? asOfDate, long? groupFilterId = null, CancellationToken ct = default);
}
