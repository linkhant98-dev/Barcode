using EMS.Application.Abstractions;
using EMS.Application.CorporateActions;
using EMS.Application.Shares;
using EMS.Application.Workflow;
using EMS.Domain.Calculations;
using EMS.Domain.Common;
using EMS.Domain.CorporateActions;
using EMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EMS.Infrastructure.Services;

/// <summary>Section 10 - dividend calculation, versioning, submission, posting and settlement.</summary>
public class DividendService : IDividendService
{
    private readonly EmsDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IReferenceNumberService _refNumbers;
    private readonly IWorkflowService _workflow;
    private readonly IAuditService _audit;

    public DividendService(
        EmsDbContext db, ICurrentUserService currentUser, IReferenceNumberService refNumbers,
        IWorkflowService workflow, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _refNumbers = refNumbers;
        _workflow = workflow;
        _audit = audit;
    }

    public async Task<long> CreateEventAsync(CreateDividendEventRequest request, CancellationToken ct = default)
    {
        var dividendEvent = new DividendEvent
        {
            DividendEventNo = await _refNumbers.NextAsync("DS", ct: ct),
            FinancialYear = request.FinancialYear,
            RecordDate = request.RecordDate,
            DividendPercentage = request.DividendPercentage,
            CapitalValuePerShare = request.CapitalValuePerShare,
            BatchVersion = $"DividendShare-{request.RecordDate:yyyyMMdd}-v1.0",
            Status = WorkflowStatus.Draft,
            MakerUserId = _currentUser.UserId,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        _db.DividendEvents.Add(dividendEvent);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Create", "DS", nameof(DividendEvent), dividendEvent.DividendEventNo, ct: ct);
        return dividendEvent.Id;
    }

    /// <summary>10.3 step 2 - old shares held the full year; new shares prorated by eligible days since record date.</summary>
    public async Task PreviewCalculationAsync(long dividendEventId, CancellationToken ct = default)
    {
        var dividendEvent = await _db.DividendEvents.Include(d => d.Entitlements)
            .FirstOrDefaultAsync(d => d.Id == dividendEventId, ct)
            ?? throw new InvalidOperationException("Dividend event not found.");

        _db.DividendEntitlements.RemoveRange(dividendEvent.Entitlements);

        var yearStart = new DateOnly(dividendEvent.RecordDate.Year, 1, 1);

        // Grouped/summed client-side - see QueryExtensions.SumDecimalAsync remarks on the SQLite dev fallback.
        var ledgerRows = await _db.ShareLedgerEntries
            .Where(l => !l.IsReversed && l.EffectiveDate <= dividendEvent.RecordDate)
            .Select(l => new { l.ShareholderId, l.EffectiveDate, l.QuantityDelta })
            .ToListAsync(ct);

        var perShareholder = ledgerRows
            .GroupBy(l => l.ShareholderId)
            .Select(g => new
            {
                ShareholderId = g.Key,
                OldShares = g.Where(x => x.EffectiveDate < yearStart).Sum(x => x.QuantityDelta),
                NewShares = g.Where(x => x.EffectiveDate >= yearStart).Sum(x => x.QuantityDelta),
                EarliestNewShareDate = g.Where(x => x.EffectiveDate >= yearStart).Select(x => (DateOnly?)x.EffectiveDate).Min()
            })
            .Where(g => g.OldShares + g.NewShares > 0)
            .ToList();

        foreach (var holding in perShareholder)
        {
            var eligibleDays = holding.EarliestNewShareDate is null
                ? 0
                : dividendEvent.RecordDate.DayNumber - holding.EarliestNewShareDate.Value.DayNumber;

            var result = DividendCalculator.Calculate(
                Math.Max(holding.OldShares, 0), Math.Max(holding.NewShares, 0), Math.Max(eligibleDays, 0),
                dividendEvent.CapitalValuePerShare, dividendEvent.DividendPercentage, 0, 0, 0);

            dividendEvent.Entitlements.Add(new DividendEntitlement
            {
                ShareholderId = holding.ShareholderId,
                OldShares = Math.Max(holding.OldShares, 0),
                NewShares = Math.Max(holding.NewShares, 0),
                EligibleDaysForNewShares = Math.Max(eligibleDays, 0),
                OldShareDividend = result.OldShareDividend,
                NewShareDividend = result.NewShareDividend,
                TotalDividend = result.TotalDividend,
                OutstandingBalance = result.TotalDividend,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId
            });
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Edit", "DS", nameof(DividendEvent), dividendEvent.DividendEventNo,
            after: new { ShareholderCount = perShareholder.Count }, ct: ct);
    }

    public async Task SubmitAsync(long dividendEventId, CancellationToken ct = default)
    {
        var dividendEvent = await _db.DividendEvents.FindAsync([dividendEventId], ct)
            ?? throw new InvalidOperationException("Dividend event not found.");

        dividendEvent.Status = WorkflowStatus.PendingApproval;
        await _db.SaveChangesAsync(ct);

        var instance = await _workflow.SubmitForApprovalAsync(new SubmitForApprovalRequest(
            nameof(DividendEvent), dividendEvent.Id, dividendEvent.DividendEventNo, "DS", null, null, null), ct);

        dividendEvent.ApprovalInstanceId = instance.Id;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>10.3 step 6 - creates payable balances after approval.</summary>
    public async Task PostApprovedAsync(long dividendEventId, CancellationToken ct = default)
    {
        var dividendEvent = await _db.DividendEvents.FindAsync([dividendEventId], ct)
            ?? throw new InvalidOperationException("Dividend event not found.");

        dividendEvent.Status = WorkflowStatus.Completed;
        dividendEvent.PostedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Decision", "DS", nameof(DividendEvent), dividendEvent.DividendEventNo, after: new { Posted = true }, ct: ct);
    }

    /// <summary>10.2/10.3 - settlement cannot exceed available balance; reinvestment reserves balance via a linked issue.</summary>
    public async Task SettleAsync(SettleDividendRequest request, CancellationToken ct = default)
    {
        var entitlement = await _db.DividendEntitlements.FindAsync([request.DividendEntitlementId], ct)
            ?? throw new InvalidOperationException("Dividend entitlement not found.");

        var settlementTotal = request.CashWithdrawal + request.AccountTransfer + request.ReinvestedAmount;
        if (settlementTotal > entitlement.OutstandingBalance + entitlement.CashWithdrawal + entitlement.AccountTransfer + entitlement.ReinvestedAmount)
            throw new InvalidOperationException("Settlement total cannot exceed the dividend entitlement.");

        entitlement.CashWithdrawal = request.CashWithdrawal;
        entitlement.AccountTransfer = request.AccountTransfer;
        entitlement.ReinvestedAmount = request.ReinvestedAmount;
        entitlement.OutstandingBalance = entitlement.TotalDividend - request.CashWithdrawal - request.AccountTransfer - request.ReinvestedAmount - entitlement.AdjustmentAmount;
        entitlement.ModifiedAtUtc = DateTime.UtcNow;
        entitlement.ModifiedBy = _currentUser.UserId;

        if (request.CashWithdrawal > 0)
            entitlement.Settlements.Add(NewSettlement(entitlement.Id, Domain.Common.DividendSettlementMethod.CashWithdrawal, request.CashWithdrawal, request.SettlementReference));
        if (request.AccountTransfer > 0)
            entitlement.Settlements.Add(NewSettlement(entitlement.Id, Domain.Common.DividendSettlementMethod.CbBankAccountTransfer, request.AccountTransfer, request.SettlementReference));
        if (request.ReinvestedAmount > 0)
            entitlement.Settlements.Add(NewSettlement(entitlement.Id, Domain.Common.DividendSettlementMethod.Reinvestment, request.ReinvestedAmount, request.SettlementReference));

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Edit", "DS", nameof(DividendEntitlement), entitlement.Id.ToString(),
            after: new { request.CashWithdrawal, request.AccountTransfer, request.ReinvestedAmount }, ct: ct);
    }

    private DividendSettlement NewSettlement(long entitlementId, Domain.Common.DividendSettlementMethod method, decimal amount, string? reference) =>
        new()
        {
            DividendEntitlementId = entitlementId,
            Method = method,
            Amount = amount,
            SettlementDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Reference = reference,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };
}
