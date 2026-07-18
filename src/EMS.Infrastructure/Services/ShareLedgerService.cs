using EMS.Application.Abstractions;
using EMS.Application.Shares;
using EMS.Domain.Shares;
using EMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EMS.Infrastructure.Services;

/// <summary>16.1 / 13.1.1 - posts immutable ledger entries and derives running balances (single source of truth).</summary>
public class ShareLedgerService : IShareLedgerService
{
    private readonly EmsDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ShareLedgerService(EmsDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task PostAsync(PostLedgerEntryRequest request, CancellationToken ct = default)
    {
        // Running balances chain from the most recently posted entry for this shareholder/class, not a sum of
        // running values (each row already carries a cumulative snapshot).
        var previousEntry = await _db.ShareLedgerEntries
            .Where(l => l.ShareholderId == request.ShareholderId && l.ShareClassId == request.ShareClassId && !l.IsReversed)
            .OrderByDescending(l => l.LedgerId)
            .FirstOrDefaultAsync(ct);

        var previousBalance = previousEntry?.RunningQuantityBalance ?? 0m;
        var previousCapital = previousEntry?.RunningPaidUpCapital ?? 0m;

        var entry = new ShareLedgerEntry
        {
            ShareholderId = request.ShareholderId,
            ShareClassId = request.ShareClassId,
            SourceTransactionId = request.SourceTransactionId,
            SourceBonusEventId = request.SourceBonusEventId,
            SourceDividendEventId = request.SourceDividendEventId,
            SourceReference = request.SourceReference,
            QuantityDelta = request.QuantityDelta,
            CapitalAmountDelta = request.CapitalAmountDelta,
            PremiumAmountDelta = request.PremiumAmountDelta,
            RunningQuantityBalance = previousBalance + request.QuantityDelta,
            RunningPaidUpCapital = previousCapital + request.CapitalAmountDelta,
            EffectiveDate = request.EffectiveDate,
            PostedAtUtc = DateTime.UtcNow,
            PostedByUserId = _currentUser.UserId
        };

        _db.ShareLedgerEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<decimal> GetCurrentBalanceAsync(long shareholderId, long shareClassId, DateOnly? asOfDate = null, CancellationToken ct = default)
    {
        var query = _db.ShareLedgerEntries
            .Where(l => l.ShareholderId == shareholderId && l.ShareClassId == shareClassId && !l.IsReversed);

        if (asOfDate is not null)
            query = query.Where(l => l.EffectiveDate <= asOfDate.Value);

        return await query.SumDecimalAsync(l => l.QuantityDelta, ct);
    }
}
