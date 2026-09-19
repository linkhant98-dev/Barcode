namespace EMS.Application.Shares;

public record PostLedgerEntryRequest(
    long ShareholderId,
    long ShareClassId,
    decimal QuantityDelta,
    decimal CapitalAmountDelta,
    decimal PremiumAmountDelta,
    string SourceReference,
    long? SourceTransactionId,
    long? SourceBonusEventId,
    long? SourceDividendEventId,
    DateOnly EffectiveDate);

/// <summary>
/// 16.1 / 13.1.1 - the single source of truth for share balances and paid-up capital. Entries are
/// immutable once posted; reversal is a new offsetting entry, never an edit (IS-BR-08, IS-BR-09).
/// </summary>
public interface IShareLedgerService
{
    Task PostAsync(PostLedgerEntryRequest request, CancellationToken ct = default);
    Task<decimal> GetCurrentBalanceAsync(long shareholderId, long shareClassId, DateOnly? asOfDate = null, CancellationToken ct = default);
}
