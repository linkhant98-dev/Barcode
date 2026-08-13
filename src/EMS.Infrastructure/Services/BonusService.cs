using EMS.Application.Abstractions;
using EMS.Application.CorporateActions;
using EMS.Application.Shares;
using EMS.Application.Workflow;
using EMS.Domain.Calculations;
using EMS.Domain.Common;
using EMS.Domain.CorporateActions;
using EMS.Domain.Shareholders;
using EMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EMS.Infrastructure.Services;

/// <summary>Section 9 - bonus share batch calculation, versioning, submission and posting.</summary>
public class BonusService : IBonusService
{
    private readonly EmsDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IReferenceNumberService _refNumbers;
    private readonly IWorkflowService _workflow;
    private readonly IShareLedgerService _ledger;
    private readonly IAuditService _audit;

    public BonusService(
        EmsDbContext db, ICurrentUserService currentUser, IReferenceNumberService refNumbers,
        IWorkflowService workflow, IShareLedgerService ledger, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _refNumbers = refNumbers;
        _workflow = workflow;
        _ledger = ledger;
        _audit = audit;
    }

    public async Task<long> CreateEventAsync(CreateBonusEventRequest request, CancellationToken ct = default)
    {
        // 9.3: only one posted bonus event per financial year/record-date combination unless an adjustment is approved.
        var duplicate = await _db.BonusEvents.AnyAsync(b =>
            b.FinancialYear == request.FinancialYear && b.RecordDate == request.RecordDate && b.Status != WorkflowStatus.Cancelled, ct);
        if (duplicate)
            throw new InvalidOperationException("A bonus event already exists for this financial year and record date.");

        var bonusEvent = new BonusEvent
        {
            BonusEventNo = await _refNumbers.NextAsync("BS", ct: ct),
            FinancialYear = request.FinancialYear,
            RecordDate = request.RecordDate,
            BonusNumerator = request.BonusNumerator,
            BonusDenominator = request.BonusDenominator,
            CashBonusRatePerRemainderShare = request.CashBonusRatePerRemainderShare,
            ShareClassId = request.ShareClassId,
            BatchVersion = $"BonusShare-{request.RecordDate:yyyyMMdd}-v1.0",
            Status = WorkflowStatus.Draft,
            MakerUserId = _currentUser.UserId,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        _db.BonusEvents.Add(bonusEvent);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Create", "BS", nameof(BonusEvent), bonusEvent.BonusEventNo, ct: ct);
        return bonusEvent.Id;
    }

    /// <summary>9.2 step 3 - preview against eligible holdings as of the record date; snapshot is retained (9.3).</summary>
    public async Task PreviewCalculationAsync(long bonusEventId, CancellationToken ct = default)
    {
        var bonusEvent = await _db.BonusEvents.Include(b => b.Entitlements)
            .FirstOrDefaultAsync(b => b.Id == bonusEventId, ct)
            ?? throw new InvalidOperationException("Bonus event not found.");

        _db.BonusEntitlements.RemoveRange(bonusEvent.Entitlements);

        // Grouped/summed client-side - see QueryExtensions.SumDecimalAsync remarks on the SQLite dev fallback.
        var ledgerRows = await _db.ShareLedgerEntries
            .Where(l => l.ShareClassId == bonusEvent.ShareClassId && !l.IsReversed && l.EffectiveDate <= bonusEvent.RecordDate)
            .Select(l => new { l.ShareholderId, l.QuantityDelta })
            .ToListAsync(ct);

        var eligibleHoldings = ledgerRows
            .GroupBy(l => l.ShareholderId)
            .Select(g => new { ShareholderId = g.Key, Shares = g.Sum(x => x.QuantityDelta) })
            .Where(g => g.Shares > 0)
            .ToList();

        foreach (var holding in eligibleHoldings)
        {
            var result = BonusCalculator.Calculate(
                holding.Shares, bonusEvent.BonusNumerator, bonusEvent.BonusDenominator, bonusEvent.CashBonusRatePerRemainderShare);

            bonusEvent.Entitlements.Add(new BonusEntitlement
            {
                ShareholderId = holding.ShareholderId,
                EligibleShares = result.EligibleShares,
                RawBonusEntitlement = result.RawBonusEntitlement,
                BonusShares = result.BonusShares,
                RemainderShares = result.RemainderShares,
                CashBonusAmount = result.CashBonusAmount,
                NewTotalShares = result.NewTotalShares
            });
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Edit", "BS", nameof(BonusEvent), bonusEvent.BonusEventNo,
            after: new { EligibleCount = eligibleHoldings.Count }, ct: ct);
    }

    public async Task SubmitAsync(long bonusEventId, CancellationToken ct = default)
    {
        var bonusEvent = await _db.BonusEvents.FindAsync([bonusEventId], ct)
            ?? throw new InvalidOperationException("Bonus event not found.");

        bonusEvent.Status = WorkflowStatus.PendingApproval;
        await _db.SaveChangesAsync(ct);

        var instance = await _workflow.SubmitForApprovalAsync(new SubmitForApprovalRequest(
            nameof(BonusEvent), bonusEvent.Id, bonusEvent.BonusEventNo, "BS", null, bonusEvent.ShareClassId, null), ct);

        bonusEvent.ApprovalInstanceId = instance.Id;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>9.2 step 7 - posts bonus share ledger entries and cash bonus obligations.</summary>
    public async Task PostApprovedAsync(long bonusEventId, CancellationToken ct = default)
    {
        var bonusEvent = await _db.BonusEvents.Include(b => b.Entitlements)
            .FirstOrDefaultAsync(b => b.Id == bonusEventId, ct)
            ?? throw new InvalidOperationException("Bonus event not found.");

        foreach (var entitlement in bonusEvent.Entitlements.Where(e => !e.IsPosted && e.BonusShares > 0))
        {
            await _ledger.PostAsync(new PostLedgerEntryRequest(
                entitlement.ShareholderId, bonusEvent.ShareClassId, entitlement.BonusShares,
                0, 0, bonusEvent.BonusEventNo, null, bonusEvent.Id, null, bonusEvent.RecordDate), ct);
            entitlement.IsPosted = true;
            entitlement.SettlementStatus = entitlement.CashBonusAmount > 0 ? "CashBonusPending" : "Posted";
        }

        bonusEvent.Status = WorkflowStatus.Completed;
        bonusEvent.PostedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Decision", "BS", nameof(BonusEvent), bonusEvent.BonusEventNo, after: new { Posted = true }, ct: ct);
    }
}
