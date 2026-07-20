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
    decimal DividendProvisionCurrentYear);

public record PendingApprovalItem(string Reference, string Module, string SubmittedBy, DateTime SubmittedAtUtc, string StepName, long ApprovalStepId);

public record RecentTransactionItem(string Reference, string Module, string Status, DateTime PostedAtUtc);

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
    int DataQualityExceptionCount);

/// <summary>Section 13.2-13.6 - the dashboard landing page: five baseline analytics plus operational widgets.</summary>
public interface IDashboardService
{
    Task<DashboardViewData> GetDashboardAsync(string? currentUserId, DateOnly? asOfDate, CancellationToken ct = default);
}
