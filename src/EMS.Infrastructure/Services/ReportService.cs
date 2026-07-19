using EMS.Application.Abstractions;
using EMS.Application.Reporting;
using EMS.Domain.Common;
using EMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EMS.Infrastructure.Services;

/// <summary>Section 13.7/13.8 - the baseline report catalogue, implemented as generic tabular queries.</summary>
public class ReportService : IReportService
{
    private readonly EmsDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissions;

    public ReportService(EmsDbContext db, ICurrentUserService currentUser, IPermissionService permissions)
    {
        _db = db;
        _currentUser = currentUser;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<(string Code, string Name, string Category)>> GetCatalogueAsync(CancellationToken ct = default) =>
        await _db.ReportDefinitions.OrderBy(r => r.ReportCode)
            .Select(r => new ValueTuple<string, string, string>(r.ReportCode, r.NameEn, r.Category))
            .ToListAsync(ct);

    public async Task<ReportResult> RunAsync(string reportCode, ReportParameters p, CancellationToken ct = default)
    {
        var definition = await _db.ReportDefinitions.FirstOrDefaultAsync(r => r.ReportCode == reportCode, ct)
            ?? throw new InvalidOperationException($"Unknown report code {reportCode}.");

        var (columns, rows, totals) = reportCode switch
        {
            "RPT-001" => await ShareholdersListAsync(ct),
            "RPT-002" => await ShareholderGroupSummaryAsync(p, ct),
            "RPT-003" => await SharesAndCertificateRecordAsync(ct),
            "RPT-004" => await PaidUpCapitalRecordAsync(ct),
            "RPT-005" => await TransferRecordAsync(ct),
            "RPT-006" => await BonusSharesAsync(ct),
            "RPT-007" => await CashBonusSummaryAsync(ct),
            "RPT-008" => await DividendSummaryAsync(p, ct),
            "RPT-009" => await DividendByPersonAsync(p, ct),
            "RPT-010" => await DividendRecordByYearAsync(ct),
            "RPT-011" => await ApprovalHistoryAsync(ct),
            "RPT-012" => await PendingApprovalAgingAsync(ct),
            "RPT-013" => await KycStatusAndAgingAsync(ct),
            "RPT-014" => await AuditTrailAsync(ct),
            "RPT-015" => await ReconciliationExceptionsAsync(ct),
            "RPT-016" => await UserAccessReviewAsync(ct),
            _ => throw new InvalidOperationException($"Report {reportCode} is not implemented.")
        };

        var filterSummary = $"As of {(p.AsOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow)):dd MMM yyyy}" +
            (p.FinancialYear is null ? "" : $" | FY {p.FinancialYear}") +
            (p.ShareholderId is null ? "" : $" | Shareholder #{p.ShareholderId}");

        return new ReportResult(reportCode, definition.NameEn, columns, rows, totals, DateTime.UtcNow, _currentUser.UserName, filterSummary);
    }

    private static Dictionary<string, object?> Row(params (string Key, object? Value)[] values) =>
        values.ToDictionary(v => v.Key, v => v.Value);

    private async Task<(List<ReportColumn>, List<IReadOnlyDictionary<string, object?>>, Dictionary<string, object?>?)> ShareholdersListAsync(CancellationToken ct)
    {
        // 13.9.5 / 11.4 - NRC/registration numbers are masked unless the caller holds Reports.ViewSensitiveData.
        var showSensitive = await _permissions.CurrentUserHasPermissionAsync(Permissions.ReportsViewSensitiveData, ct);
        var columns = new List<ReportColumn>
        {
            new("ShareholderNo", "Shareholder ID"), new("Name", "Name"), new("Type", "Type"),
            new("NrcOrReg", "NRC / Registration"), new("Group", "Group"), new("Status", "Status"),
            new("RegistrationDate", "Registration Date"), new("TotalShares", "Total Shares", true)
        };

        var shareholders = await _db.Shareholders
            .Include(s => s.Person).Include(s => s.Corporate).Include(s => s.ShareholderGroup)
            .OrderBy(s => s.ShareholderNo).ToListAsync(ct);

        var ledger = await _db.ShareLedgerEntries.Where(l => !l.IsReversed)
            .Select(l => new { l.ShareholderId, l.QuantityDelta }).ToListAsync(ct);
        var totalsByShareholder = ledger.GroupBy(l => l.ShareholderId).ToDictionary(g => g.Key, g => g.Sum(x => x.QuantityDelta));

        var rows = shareholders.Select(s => (IReadOnlyDictionary<string, object?>)Row(
            ("ShareholderNo", s.ShareholderNo),
            ("Name", s.Type == ApplicantType.Corporate ? s.Corporate?.LegalNameEn : s.Person?.NameEn),
            ("Type", s.Type.ToString()),
            ("NrcOrReg", showSensitive ? (s.Person?.NrcNumber ?? s.Corporate?.RegistrationNumber) : MaskSensitive(s.Person?.NrcNumber ?? s.Corporate?.RegistrationNumber)),
            ("Group", s.ShareholderGroup?.NameEn),
            ("Status", s.Status.ToString()),
            ("RegistrationDate", s.RegistrationDate.ToString("dd MMM yyyy")),
            ("TotalShares", totalsByShareholder.GetValueOrDefault(s.Id, 0m))
        )).ToList();

        return (columns, rows, new Dictionary<string, object?> { ["TotalShares"] = rows.Sum(r => (decimal)(r["TotalShares"] ?? 0m)) });
    }

    private async Task<(List<ReportColumn>, List<IReadOnlyDictionary<string, object?>>, Dictionary<string, object?>?)> ShareholderGroupSummaryAsync(ReportParameters p, CancellationToken ct)
    {
        var columns = new List<ReportColumn>
        {
            new("Group", "Group"), new("HolderCount", "Holder Count", true), new("TotalShares", "Total Shares", true),
            new("Percentage", "% of Total", true), new("PaidUpCapital", "Paid-up Capital", true)
        };

        var cutoff = p.AsOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var ledgerRows = await _db.ShareLedgerEntries.Where(l => !l.IsReversed && l.EffectiveDate <= cutoff)
            .Select(l => new { l.ShareholderId, l.QuantityDelta, l.CapitalAmountDelta }).ToListAsync(ct);
        var groupsByShareholder = await _db.Shareholders.Select(s => new { s.Id, GroupName = s.ShareholderGroup!.NameEn }).ToListAsync(ct);
        var groupLookup = groupsByShareholder.ToDictionary(g => g.Id, g => g.GroupName);

        var total = ledgerRows.Sum(l => l.QuantityDelta);

        var grouped = ledgerRows.GroupBy(l => groupLookup.GetValueOrDefault(l.ShareholderId, "Unassigned"))
            .Select(g => new
            {
                Group = g.Key,
                Holders = g.Select(x => x.ShareholderId).Distinct().Count(),
                Shares = g.Sum(x => x.QuantityDelta),
                Capital = g.Sum(x => x.CapitalAmountDelta)
            })
            .OrderByDescending(g => g.Shares)
            .ToList();

        var rows = grouped.Select(g => (IReadOnlyDictionary<string, object?>)Row(
            ("Group", g.Group), ("HolderCount", g.Holders), ("TotalShares", g.Shares),
            ("Percentage", total > 0 ? Math.Round(g.Shares / total * 100m, 2) : 0m), ("PaidUpCapital", g.Capital)
        )).ToList();

        var totals = new Dictionary<string, object?>
        {
            ["HolderCount"] = grouped.Sum(g => g.Holders), ["TotalShares"] = total,
            ["Percentage"] = 100m, ["PaidUpCapital"] = grouped.Sum(g => g.Capital)
        };

        return (columns, rows, totals);
    }

    private async Task<(List<ReportColumn>, List<IReadOnlyDictionary<string, object?>>, Dictionary<string, object?>?)> SharesAndCertificateRecordAsync(CancellationToken ct)
    {
        var columns = new List<ReportColumn>
        {
            new("Shareholder", "Shareholder"), new("Class", "Class"), new("CertificateNumber", "Certificate Number"),
            new("Quantity", "Quantity", true), new("IssueDate", "Issue Date"), new("Status", "Status")
        };

        var certs = await _db.ShareCertificates.Include(c => c.Shareholder).Include(c => c.ShareClass)
            .OrderByDescending(c => c.IssueDate).ToListAsync(ct);

        var rows = certs.Select(c => (IReadOnlyDictionary<string, object?>)Row(
            ("Shareholder", c.Shareholder?.ShareholderNo), ("Class", c.ShareClass?.NameEn),
            ("CertificateNumber", c.CertificateNumber), ("Quantity", c.Quantity),
            ("IssueDate", c.IssueDate.ToString("dd MMM yyyy")), ("Status", c.Status.ToString())
        )).ToList();

        return (columns, rows, new Dictionary<string, object?> { ["Quantity"] = rows.Sum(r => (decimal)(r["Quantity"] ?? 0m)) });
    }

    private async Task<(List<ReportColumn>, List<IReadOnlyDictionary<string, object?>>, Dictionary<string, object?>?)> PaidUpCapitalRecordAsync(CancellationToken ct)
    {
        var columns = new List<ReportColumn>
        {
            new("PostingDate", "Posting Date"), new("Reference", "Reference"), new("Shareholder", "Shareholder"),
            new("CapitalAmount", "Capital Amount", true), new("RunningCapital", "Running Paid-up Capital", true)
        };

        var entries = await _db.ShareLedgerEntries.Include(l => l.Shareholder).Where(l => !l.IsReversed)
            .OrderBy(l => l.EffectiveDate).ToListAsync(ct);

        var rows = entries.Select(e => (IReadOnlyDictionary<string, object?>)Row(
            ("PostingDate", e.EffectiveDate.ToString("dd MMM yyyy")), ("Reference", e.SourceReference),
            ("Shareholder", e.Shareholder?.ShareholderNo), ("CapitalAmount", e.CapitalAmountDelta),
            ("RunningCapital", e.RunningPaidUpCapital)
        )).ToList();

        return (columns, rows, new Dictionary<string, object?> { ["CapitalAmount"] = rows.Sum(r => (decimal)(r["CapitalAmount"] ?? 0m)) });
    }

    private async Task<(List<ReportColumn>, List<IReadOnlyDictionary<string, object?>>, Dictionary<string, object?>?)> TransferRecordAsync(CancellationToken ct)
    {
        var columns = new List<ReportColumn>
        {
            new("Reference", "Reference"), new("Date", "Transfer Date"), new("From", "From"), new("To", "To"),
            new("Type", "Type"), new("Quantity", "Quantity", true), new("Status", "Status")
        };

        var transfers = await _db.ShareTransactions.Include(t => t.ShareTransfer)!.ThenInclude(x => x!.FromShareholder)
            .Include(t => t.ShareTransfer)!.ThenInclude(x => x!.ToShareholder)
            .Where(t => t.Type == ShareTransactionType.TransferShares)
            .OrderByDescending(t => t.EffectiveDate).ToListAsync(ct);

        var rows = transfers.Select(t => (IReadOnlyDictionary<string, object?>)Row(
            ("Reference", t.TransactionNo), ("Date", t.EffectiveDate.ToString("dd MMM yyyy")),
            ("From", t.ShareTransfer?.FromShareholder?.ShareholderNo), ("To", t.ShareTransfer?.ToShareholder?.ShareholderNo),
            ("Type", t.ShareTransfer?.TransferType.ToString()), ("Quantity", t.ShareTransfer?.Quantity ?? 0m),
            ("Status", t.Status.ToString())
        )).ToList();

        return (columns, rows, new Dictionary<string, object?> { ["Quantity"] = rows.Sum(r => (decimal)(r["Quantity"] ?? 0m)) });
    }

    private async Task<(List<ReportColumn>, List<IReadOnlyDictionary<string, object?>>, Dictionary<string, object?>?)> BonusSharesAsync(CancellationToken ct)
    {
        var columns = new List<ReportColumn>
        {
            new("Event", "Event"), new("Year", "Financial Year"), new("Shareholder", "Shareholder"),
            new("EligibleShares", "Eligible Shares", true), new("BonusShares", "Bonus Shares", true), new("Status", "Status")
        };

        var entitlements = await _db.BonusEntitlements.Include(e => e.BonusEvent).Include(e => e.Shareholder)
            .OrderByDescending(e => e.BonusEventId).ToListAsync(ct);

        var rows = entitlements.Select(e => (IReadOnlyDictionary<string, object?>)Row(
            ("Event", e.BonusEvent?.BonusEventNo), ("Year", e.BonusEvent?.FinancialYear),
            ("Shareholder", e.Shareholder?.ShareholderNo), ("EligibleShares", e.EligibleShares),
            ("BonusShares", e.BonusShares), ("Status", e.BonusEvent?.Status.ToString())
        )).ToList();

        return (columns, rows, new Dictionary<string, object?> { ["BonusShares"] = rows.Sum(r => (decimal)(r["BonusShares"] ?? 0m)) });
    }

    private async Task<(List<ReportColumn>, List<IReadOnlyDictionary<string, object?>>, Dictionary<string, object?>?)> CashBonusSummaryAsync(CancellationToken ct)
    {
        var columns = new List<ReportColumn>
        {
            new("Event", "Event"), new("Shareholder", "Shareholder"), new("Remainder", "Remainder Shares", true),
            new("CashBonus", "Cash Bonus Amount", true), new("SettlementStatus", "Status")
        };

        var entitlements = await _db.BonusEntitlements.Include(e => e.BonusEvent).Include(e => e.Shareholder)
            .Where(e => e.CashBonusAmount > 0)
            .OrderByDescending(e => e.BonusEventId).ToListAsync(ct);

        var rows = entitlements.Select(e => (IReadOnlyDictionary<string, object?>)Row(
            ("Event", e.BonusEvent?.BonusEventNo), ("Shareholder", e.Shareholder?.ShareholderNo),
            ("Remainder", e.RemainderShares), ("CashBonus", e.CashBonusAmount), ("SettlementStatus", e.SettlementStatus ?? "Pending")
        )).ToList();

        return (columns, rows, new Dictionary<string, object?> { ["CashBonus"] = rows.Sum(r => (decimal)(r["CashBonus"] ?? 0m)) });
    }

    private async Task<(List<ReportColumn>, List<IReadOnlyDictionary<string, object?>>, Dictionary<string, object?>?)> DividendSummaryAsync(ReportParameters p, CancellationToken ct)
    {
        var columns = new List<ReportColumn>
        {
            new("Event", "Event"), new("Year", "Financial Year"), new("Shareholder", "Shareholder"),
            new("TotalDividend", "Total Dividend", true), new("Outstanding", "Outstanding", true)
        };

        var query = _db.DividendEntitlements.Include(e => e.DividendEvent).Include(e => e.Shareholder).AsQueryable();
        if (p.FinancialYear is not null) query = query.Where(e => e.DividendEvent!.FinancialYear == p.FinancialYear);

        var entitlements = await query.OrderByDescending(e => e.DividendEventId).ToListAsync(ct);

        var rows = entitlements.Select(e => (IReadOnlyDictionary<string, object?>)Row(
            ("Event", e.DividendEvent?.DividendEventNo), ("Year", e.DividendEvent?.FinancialYear),
            ("Shareholder", e.Shareholder?.ShareholderNo), ("TotalDividend", e.TotalDividend), ("Outstanding", e.OutstandingBalance)
        )).ToList();

        return (columns, rows, new Dictionary<string, object?>
        {
            ["TotalDividend"] = rows.Sum(r => (decimal)(r["TotalDividend"] ?? 0m)),
            ["Outstanding"] = rows.Sum(r => (decimal)(r["Outstanding"] ?? 0m))
        });
    }

    private async Task<(List<ReportColumn>, List<IReadOnlyDictionary<string, object?>>, Dictionary<string, object?>?)> DividendByPersonAsync(ReportParameters p, CancellationToken ct)
    {
        var columns = new List<ReportColumn>
        {
            new("Year", "Financial Year"), new("OldShares", "Old Shares", true), new("NewShares", "New Shares", true),
            new("TotalDividend", "Total Dividend", true), new("CashWithdrawal", "Cash Withdrawal", true),
            new("AccountTransfer", "Account Transfer", true), new("Reinvested", "Reinvested", true), new("Outstanding", "Outstanding", true)
        };

        var entitlements = p.ShareholderId is null
            ? new List<Domain.CorporateActions.DividendEntitlement>()
            : await _db.DividendEntitlements.Include(e => e.DividendEvent)
                .Where(e => e.ShareholderId == p.ShareholderId).OrderBy(e => e.DividendEvent!.FinancialYear).ToListAsync(ct);

        var rows = entitlements.Select(e => (IReadOnlyDictionary<string, object?>)Row(
            ("Year", e.DividendEvent?.FinancialYear), ("OldShares", e.OldShares), ("NewShares", e.NewShares),
            ("TotalDividend", e.TotalDividend), ("CashWithdrawal", e.CashWithdrawal),
            ("AccountTransfer", e.AccountTransfer), ("Reinvested", e.ReinvestedAmount), ("Outstanding", e.OutstandingBalance)
        )).ToList();

        return (columns, rows, null);
    }

    private async Task<(List<ReportColumn>, List<IReadOnlyDictionary<string, object?>>, Dictionary<string, object?>?)> DividendRecordByYearAsync(CancellationToken ct)
    {
        var columns = new List<ReportColumn>
        {
            new("Year", "Financial Year"), new("Rate", "Dividend %", true), new("TotalProvision", "Total Provision", true),
            new("Outstanding", "Outstanding", true), new("Status", "Status")
        };

        var events = await _db.DividendEvents.OrderBy(d => d.FinancialYear).ToListAsync(ct);
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        foreach (var d in events)
        {
            var entitlements = await _db.DividendEntitlements.Where(e => e.DividendEventId == d.Id)
                .Select(e => new { e.TotalDividend, e.OutstandingBalance }).ToListAsync(ct);

            rows.Add(Row(
                ("Year", d.FinancialYear), ("Rate", d.DividendPercentage),
                ("TotalProvision", entitlements.Sum(e => e.TotalDividend)),
                ("Outstanding", entitlements.Sum(e => e.OutstandingBalance)), ("Status", d.Status.ToString())
            ));
        }

        return (columns, rows, null);
    }

    private async Task<(List<ReportColumn>, List<IReadOnlyDictionary<string, object?>>, Dictionary<string, object?>?)> ApprovalHistoryAsync(CancellationToken ct)
    {
        var columns = new List<ReportColumn>
        {
            new("Reference", "Reference"), new("Module", "Module"), new("Step", "Step"), new("Decision", "Decision"),
            new("DecisionBy", "Decided By"), new("DecisionDate", "Decision Date"), new("Comment", "Remark")
        };

        var steps = await _db.ApprovalSteps.Include(s => s.ApprovalInstance)
            .Where(s => s.Decision != null)
            .OrderByDescending(s => s.DecisionAtUtc).ToListAsync(ct);

        var rows = steps.Select(s => (IReadOnlyDictionary<string, object?>)Row(
            ("Reference", s.ApprovalInstance?.EntityReference), ("Module", s.ApprovalInstance?.EntityType),
            ("Step", s.StepName), ("Decision", s.Decision?.ToString()), ("DecisionBy", s.DecisionByUserId),
            ("DecisionDate", s.DecisionAtUtc?.ToString("dd MMM yyyy HH:mm")), ("Comment", s.Comment)
        )).ToList();

        return (columns, rows, null);
    }

    private async Task<(List<ReportColumn>, List<IReadOnlyDictionary<string, object?>>, Dictionary<string, object?>?)> PendingApprovalAgingAsync(CancellationToken ct)
    {
        var columns = new List<ReportColumn>
        {
            new("Reference", "Reference"), new("Module", "Module"), new("Step", "Step"),
            new("SubmittedDate", "Submitted"), new("AgeDays", "Age (days)", true)
        };

        var steps = await _db.ApprovalSteps.Include(s => s.ApprovalInstance)
            .Where(s => s.Status == ApprovalStepStatus.Pending)
            .ToListAsync(ct);

        var rows = steps.Select(s => (IReadOnlyDictionary<string, object?>)Row(
            ("Reference", s.ApprovalInstance?.EntityReference), ("Module", s.ApprovalInstance?.EntityType),
            ("Step", s.StepName), ("SubmittedDate", s.ApprovalInstance?.SubmittedAtUtc.ToString("dd MMM yyyy")),
            ("AgeDays", (int)(DateTime.UtcNow - (s.ApprovalInstance?.SubmittedAtUtc ?? DateTime.UtcNow)).TotalDays)
        )).OrderByDescending(r => (int)(r["AgeDays"] ?? 0)).ToList();

        return (columns, rows, null);
    }

    private async Task<(List<ReportColumn>, List<IReadOnlyDictionary<string, object?>>, Dictionary<string, object?>?)> KycStatusAndAgingAsync(CancellationToken ct)
    {
        var columns = new List<ReportColumn>
        {
            new("Application", "Application"), new("Result", "KYC Result"), new("Requested", "Requested"),
            new("Decided", "Decided"), new("Officer", "Decision User")
        };

        var cases = await _db.KycCases.Include(k => k.ShareholderApplication).OrderByDescending(k => k.RequestedAtUtc).ToListAsync(ct);

        var rows = cases.Select(k => (IReadOnlyDictionary<string, object?>)Row(
            ("Application", k.ShareholderApplication?.ApplicationNo), ("Result", k.Result.ToString()),
            ("Requested", k.RequestedAtUtc.ToString("dd MMM yyyy")), ("Decided", k.DecisionAtUtc?.ToString("dd MMM yyyy")),
            ("Officer", k.DecisionUserId)
        )).ToList();

        return (columns, rows, null);
    }

    private async Task<(List<ReportColumn>, List<IReadOnlyDictionary<string, object?>>, Dictionary<string, object?>?)> AuditTrailAsync(CancellationToken ct)
    {
        var columns = new List<ReportColumn>
        {
            new("Timestamp", "Timestamp"), new("User", "User"), new("Action", "Action"),
            new("Module", "Module"), new("Reference", "Reference"), new("Result", "Result")
        };

        var entries = await _db.AuditLogEntries.OrderByDescending(a => a.TimestampUtc).Take(500).ToListAsync(ct);

        var rows = entries.Select(a => (IReadOnlyDictionary<string, object?>)Row(
            ("Timestamp", a.TimestampUtc.ToString("dd MMM yyyy HH:mm:ss")), ("User", a.UserName), ("Action", a.Action),
            ("Module", a.Module), ("Reference", a.EntityReference), ("Result", a.Result)
        )).ToList();

        return (columns, rows, null);
    }

    private async Task<(List<ReportColumn>, List<IReadOnlyDictionary<string, object?>>, Dictionary<string, object?>?)> ReconciliationExceptionsAsync(CancellationToken ct)
    {
        var columns = new List<ReportColumn>
        {
            new("Control", "Control"), new("BusinessDate", "Business Date"), new("Expected", "Expected", true),
            new("Actual", "Actual", true), new("Difference", "Difference", true), new("Status", "Status")
        };

        var results = await _db.ReconciliationResults.OrderByDescending(r => r.BusinessDate).ToListAsync(ct);

        var rows = results.Select(r => (IReadOnlyDictionary<string, object?>)Row(
            ("Control", r.ControlName), ("BusinessDate", r.BusinessDate.ToString("dd MMM yyyy")),
            ("Expected", r.ExpectedValue), ("Actual", r.ActualValue), ("Difference", r.Difference), ("Status", r.Status)
        )).ToList();

        return (columns, rows, null);
    }

    private async Task<(List<ReportColumn>, List<IReadOnlyDictionary<string, object?>>, Dictionary<string, object?>?)> UserAccessReviewAsync(CancellationToken ct)
    {
        var columns = new List<ReportColumn>
        {
            new("UserName", "Username"), new("FullName", "Full Name"), new("Email", "Email"),
            new("Status", "Status"), new("LastLogin", "Last Login")
        };

        var users = await _db.Users.OrderBy(u => u.UserName).ToListAsync(ct);

        var rows = users.Select(u => (IReadOnlyDictionary<string, object?>)Row(
            ("UserName", u.UserName), ("FullName", u.FullName), ("Email", u.Email),
            ("Status", u.Status.ToString()), ("LastLogin", u.LastLoginUtc?.ToString("dd MMM yyyy HH:mm") ?? "Never")
        )).ToList();

        return (columns, rows, null);
    }

    /// <summary>11.4 - mask NRC/registration values in list screens; a real deployment ties this to role/permission.</summary>
    private static string? MaskSensitive(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= 4) return value;
        return new string('*', value.Length - 4) + value[^4..];
    }
}
