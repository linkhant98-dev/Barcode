using EMS.Application.Reporting;
using EMS.Domain.Common;
using EMS.Domain.Workflow;
using EMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EMS.Infrastructure.Services;

/// <summary>
/// Section 13.2-13.6 dashboard queries. Every total is derived directly from posted ShareLedgerEntry /
/// DividendEntitlement rows (13.1.1 single source of truth) - never a separately maintained reporting balance.
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly EmsDbContext _db;

    public DashboardService(EmsDbContext db) => _db = db;

    public async Task<DashboardViewData> GetDashboardAsync(string? currentUserId, DateOnly? asOfDate, CancellationToken ct = default)
    {
        var cutoff = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        // Fetch once and aggregate client-side: the SQLite dev-fallback provider cannot translate SUM/GROUP BY
        // over decimal columns server-side (SQL Server has no such restriction), and dashboard/report volumes
        // here are small enough that this is correct and simple on both providers (13.9.2 still holds for SQL
        // Server, where the same LINQ compiles to server-side SUM).
        var ledgerRows = await _db.ShareLedgerEntries
            .Where(l => !l.IsReversed && l.EffectiveDate <= cutoff)
            .Select(l => new { l.ShareholderId, l.EffectiveDate, l.QuantityDelta, l.CapitalAmountDelta })
            .ToListAsync(ct);

        var totalShares = ledgerRows.Sum(l => l.QuantityDelta);
        var totalCapital = ledgerRows.Sum(l => l.CapitalAmountDelta);

        // DB-01 / DB-02 - shareholding by group, count and percentage.
        var shareholderGroupNames = await _db.Shareholders
            .Select(s => new { s.Id, GroupName = s.ShareholderGroup!.NameEn })
            .ToListAsync(ct);
        var groupNameByShareholder = shareholderGroupNames.ToDictionary(s => s.Id, s => s.GroupName);

        var byGroup = ledgerRows
            .GroupBy(l => groupNameByShareholder.GetValueOrDefault(l.ShareholderId, "Unassigned"))
            .Select(g => new { NameEn = g.Key, Shares = g.Sum(x => x.QuantityDelta), Holders = g.Select(x => x.ShareholderId).Distinct().Count() })
            .Where(g => g.Shares > 0)
            .OrderByDescending(g => g.Shares)
            .Select(g => new ShareholdingByGroupItem(
                g.NameEn, g.Shares, g.Holders,
                totalShares > 0 ? Math.Round(g.Shares / totalShares * 100m, 2) : 0m))
            .ToList();

        // DB-03 - yearly paid-up capital comparison (closing balance per calendar/financial year).
        var yearlyCapitalRaw = ledgerRows
            .GroupBy(l => l.EffectiveDate.Year)
            .Select(g => new { Year = g.Key, Capital = g.Sum(x => x.CapitalAmountDelta) })
            .OrderBy(g => g.Year)
            .ToList();

        decimal running = 0m;
        var yearlyCapital = new List<YearlyCapitalItem>();
        foreach (var y in yearlyCapitalRaw)
        {
            running += y.Capital;
            yearlyCapital.Add(new YearlyCapitalItem(y.Year.ToString(), running));
        }

        // DB-04 - yearly dividend percentage and provision amount, from posted dividend events.
        var dividendEvents = await _db.DividendEvents
            .Where(d => d.Status == WorkflowStatus.Completed || d.Status == WorkflowStatus.Approved)
            .OrderBy(d => d.FinancialYear)
            .Select(d => new { d.FinancialYear, d.DividendPercentage, d.Id })
            .ToListAsync(ct);

        var yearlyDividend = new List<YearlyDividendItem>();
        foreach (var d in dividendEvents)
        {
            var provision = (await _db.DividendEntitlements.Where(e => e.DividendEventId == d.Id)
                .Select(e => e.TotalDividend).ToListAsync(ct)).Sum();
            yearlyDividend.Add(new YearlyDividendItem(d.FinancialYear, d.DividendPercentage, provision));
        }

        // DB-05 - dividend comparison (settlement composition) by year.
        var dividendComparison = new List<DividendComparisonItem>();
        foreach (var d in dividendEvents)
        {
            var totals = await _db.DividendEntitlements.Where(e => e.DividendEventId == d.Id)
                .Select(e => new { e.CashWithdrawal, e.AccountTransfer, e.ReinvestedAmount, e.OutstandingBalance })
                .ToListAsync(ct);

            dividendComparison.Add(new DividendComparisonItem(
                d.FinancialYear,
                totals.Sum(t => t.CashWithdrawal),
                totals.Sum(t => t.AccountTransfer),
                totals.Sum(t => t.ReinvestedAmount),
                totals.Sum(t => t.OutstandingBalance)));
        }

        // DB-06/DB-07 - pending KYC / pending approvals.
        var pendingKycCount = await _db.KycCases.CountAsync(k => k.Result == KycResult.Pending, ct);

        var myPendingApprovals = new List<PendingApprovalItem>();
        if (!string.IsNullOrEmpty(currentUserId))
        {
            myPendingApprovals = await (
                from step in _db.ApprovalSteps
                join instance in _db.ApprovalInstances on step.ApprovalInstanceId equals instance.Id
                where step.Status == ApprovalStepStatus.Pending && instance.Status == WorkflowStatus.PendingApproval
                orderby instance.SubmittedAtUtc
                select new PendingApprovalItem(
                    instance.EntityReference, instance.EntityType, instance.SubmittedByUserId, instance.SubmittedAtUtc, step.StepName, step.Id))
                .Take(5)
                .ToListAsync(ct);
        }

        // DB-08 - recent transactions.
        var recentTransactions = await _db.ShareTransactions
            .OrderByDescending(t => t.Id)
            .Take(5)
            .Select(t => new RecentTransactionItem(t.TransactionNo, t.Type.ToString(), t.Status.ToString(), t.ModifiedAtUtc ?? t.CreatedAtUtc))
            .ToListAsync(ct);

        // DB-10 - simple data-quality exception count (duplicate active certificate numbers).
        var duplicateCertificates = await _db.ShareCertificates
            .Where(c => c.Status != Domain.Common.CertificateStatus.Cancelled)
            .GroupBy(c => c.CertificateNumber)
            .Where(g => g.Count() > 1)
            .CountAsync(ct);

        var kpis = new DashboardKpis(
            RegisteredShareholders: await _db.Shareholders.CountAsync(s => s.Status == Domain.Common.ShareholderStatus.Active, ct),
            TotalShares: totalShares,
            PaidUpCapital: totalCapital,
            PendingApprovals: await _db.ApprovalInstances.CountAsync(i => i.Status == WorkflowStatus.PendingApproval, ct),
            DividendProvisionCurrentYear: yearlyDividend.LastOrDefault()?.ProvisionAmount ?? 0m);

        return new DashboardViewData(
            DateTime.UtcNow, kpis, byGroup, yearlyCapital, yearlyDividend, dividendComparison,
            myPendingApprovals, recentTransactions, pendingKycCount, duplicateCertificates);
    }
}
